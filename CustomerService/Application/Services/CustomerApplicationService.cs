using CustomerService.Application.DTOs.Addresses;
using CustomerService.Application.DTOs.Customers;
using CustomerService.Application.Interfaces;
using CustomerService.Application.Models;
using CustomerService.Application.Validation;
using CustomerService.Common.Exceptions;
using CustomerService.Domain.Entities;
using CustomerService.Domain.Enums;
using CustomerService.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CustomerService.Application.Services;

public sealed class CustomerApplicationService(
    ICustomerRepository customers,
    ICustomerAddressRepository addresses,
    CustomerDbContext db,
    TimeProvider timeProvider,
    ILogger<CustomerApplicationService> logger) : ICustomerService
{
    public async Task<PagedResponse<CustomerSummaryResponse>> GetAsync(CustomerQueryParameters query, CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var source = customers.Query();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            source = source.Where(x => x.CustomerNumber.ToUpper().Contains(search)
                || x.FirstName.ToUpper().Contains(search)
                || x.LastName.ToUpper().Contains(search)
                || (x.FirstName + " " + x.LastName).ToUpper().Contains(search)
                || x.Email.ToUpper().Contains(search)
                || (x.PhoneNumber != null && x.PhoneNumber.Contains(search)));
        }
        if (query.Status.HasValue) source = source.Where(x => x.Status == query.Status.Value);
        if (query.HasAddresses.HasValue) source = source.Where(x => x.Addresses.Any() == query.HasAddresses.Value);
        if (query.CreatedFromUtc.HasValue) source = source.Where(x => x.CreatedAtUtc >= query.CreatedFromUtc.Value);
        if (query.CreatedToUtc.HasValue) source = source.Where(x => x.CreatedAtUtc <= query.CreatedToUtc.Value);

        var ascending = string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
        source = query.SortBy.ToLowerInvariant() switch
        {
            "customernumber" => ascending ? source.OrderBy(x => x.CustomerNumber) : source.OrderByDescending(x => x.CustomerNumber),
            "firstname" => ascending ? source.OrderBy(x => x.FirstName) : source.OrderByDescending(x => x.FirstName),
            "lastname" => ascending ? source.OrderBy(x => x.LastName) : source.OrderByDescending(x => x.LastName),
            "email" => ascending ? source.OrderBy(x => x.NormalizedEmail) : source.OrderByDescending(x => x.NormalizedEmail),
            "status" => ascending ? source.OrderBy(x => x.Status) : source.OrderByDescending(x => x.Status),
            "updatedat" => ascending ? source.OrderBy(x => x.UpdatedAtUtc) : source.OrderByDescending(x => x.UpdatedAtUtc),
            _ => ascending ? source.OrderBy(x => x.CreatedAtUtc) : source.OrderByDescending(x => x.CreatedAtUtc)
        };

        var total = await source.LongCountAsync(cancellationToken);
        var items = await source.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize)
            .Select(x => new CustomerSummaryResponse(x.Id, x.CustomerNumber, x.FirstName + " " + x.LastName, x.Email, x.PhoneNumber, x.Status, x.Addresses.Count, x.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        return PagedResponse<CustomerSummaryResponse>.Create(items, query.PageNumber, query.PageSize, total);
    }

    public async Task<CustomerDetailsResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        MapDetails(await customers.GetDetailsByIdAsync(id, cancellationToken) ?? throw new NotFoundException($"Customer '{id}' was not found."));

    public async Task<CustomerDetailsResponse> GetByCustomerNumberAsync(string customerNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerNumber)) throw Validation("customerNumber", "customerNumber is required.");
        return MapDetails(await customers.GetByCustomerNumberAsync(customerNumber.Trim(), cancellationToken)
            ?? throw new NotFoundException($"Customer number '{customerNumber}' was not found."));
    }

    public async Task<CustomerDetailsResponse> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var normalized = EmailNormalizer.Normalize(email).NormalizedEmail;
        return MapDetails(await customers.GetByEmailAsync(normalized, cancellationToken) ?? throw new NotFoundException("Customer was not found."));
    }

    public async Task<CustomerDetailsResponse> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        ValidateName(request.FirstName, nameof(request.FirstName));
        ValidateName(request.LastName, nameof(request.LastName));
        var email = EmailNormalizer.Normalize(request.Email);
        var phone = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        if (await customers.EmailExistsAsync(email.NormalizedEmail, null, cancellationToken))
        {
            logger.LogWarning("Duplicate customer email was rejected");
            throw new DuplicateEmailException("A customer with this email already exists.");
        }

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            var now = timeProvider.GetUtcNow();
            var customer = new Customer
            {
                Id = Guid.NewGuid(), CustomerNumber = GenerateCustomerNumber(), FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(),
                Email = email.Email, NormalizedEmail = email.NormalizedEmail, PhoneNumber = phone, Status = CustomerStatus.Active,
                CreatedAtUtc = now, ConcurrencyToken = Guid.NewGuid()
            };
            try
            {
                if (request.InitialAddress is null)
                {
                    await customers.AddAsync(customer, cancellationToken);
                    await customers.SaveChangesAsync(cancellationToken);
                }
                else
                {
                    AddressRules.Validate(request.InitialAddress);
                    await ExecuteTransactionAsync(async () =>
                    {
                        await customers.AddAsync(customer, cancellationToken);
                        var address = AddressRules.Create(customer.Id, customer, request.InitialAddress, now, forceDefaults: true);
                        await addresses.AddAsync(address, cancellationToken);
                        await customers.SaveChangesAsync(cancellationToken);
                    }, cancellationToken);
                }
                logger.LogInformation("Created customer {CustomerId} with number {CustomerNumber}", customer.Id, customer.CustomerNumber);
                return await GetByIdAsync(customer.Id, cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "CustomerNumber") && attempt < 5)
            {
                db.ChangeTracker.Clear();
                logger.LogWarning("Customer number collision on generation attempt {Attempt}", attempt);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, "NormalizedEmail"))
            {
                logger.LogWarning("Duplicate customer email was rejected during save");
                throw new DuplicateEmailException("A customer with this email already exists.");
            }
        }
        throw new ConflictException("A unique customer number could not be generated. Retry the request.");
    }

    public async Task<CustomerDetailsResponse> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        ValidateName(request.FirstName, nameof(request.FirstName));
        ValidateName(request.LastName, nameof(request.LastName));
        var customer = await customers.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Customer '{id}' was not found.");
        EnsureConcurrency(customer.ConcurrencyToken, request.ConcurrencyToken, "Customer data was modified by another request.");
        var email = EmailNormalizer.Normalize(request.Email);
        if (await customers.EmailExistsAsync(email.NormalizedEmail, id, cancellationToken)) throw new DuplicateEmailException("A customer with this email already exists.");
        customer.FirstName = request.FirstName.Trim();
        customer.LastName = request.LastName.Trim();
        customer.Email = email.Email;
        customer.NormalizedEmail = email.NormalizedEmail;
        customer.PhoneNumber = PhoneNumberNormalizer.Normalize(request.PhoneNumber);
        Touch(customer);
        try { await customers.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("Customer data was modified by another request."); }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception, "NormalizedEmail")) { throw new DuplicateEmailException("A customer with this email already exists."); }
        logger.LogInformation("Updated customer {CustomerId}", id);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<CustomerDetailsResponse> UpdateStatusAsync(Guid id, UpdateCustomerStatusRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Status)) throw Validation("status", "The customer status is invalid.");
        var customer = await customers.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Customer '{id}' was not found.");
        EnsureConcurrency(customer.ConcurrencyToken, request.ConcurrencyToken, "Customer data was modified by another request.");
        customer.Status = request.Status;
        Touch(customer);
        await SaveCustomerAsync(cancellationToken);
        logger.LogInformation("Changed status for customer {CustomerId} to {Status}; reason supplied: {ReasonSupplied}", id, request.Status, !string.IsNullOrWhiteSpace(request.Reason));
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeleteAsync(Guid id, Guid concurrencyToken, CancellationToken cancellationToken)
    {
        var customer = await customers.GetTrackedAsync(id, cancellationToken) ?? throw new NotFoundException($"Customer '{id}' was not found.");
        EnsureConcurrency(customer.ConcurrencyToken, concurrencyToken, "Customer data was modified by another request.");
        customer.IsDeleted = true;
        customer.Status = CustomerStatus.Deactivated;
        customer.DeletedAtUtc = timeProvider.GetUtcNow();
        Touch(customer);
        await SaveCustomerAsync(cancellationToken);
        logger.LogInformation("Soft-deleted customer {CustomerId} with number {CustomerNumber}", id, customer.CustomerNumber);
    }

    public async Task<CustomerEligibilityResponse> GetEligibilityAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await customers.Query().Where(x => x.Id == id).Select(x => new
        {
            x.Status,
            HasShipping = x.Addresses.Any(a => a.IsDefaultShipping),
            HasBilling = x.Addresses.Any(a => a.IsDefaultBilling)
        }).SingleOrDefaultAsync(cancellationToken);
        if (item is null) return new(id, false, null, false, false, false, false);
        var canCart = item.Status is CustomerStatus.Active or CustomerStatus.Suspended;
        var canOrder = item.Status == CustomerStatus.Active;
        if (!canOrder) logger.LogInformation("Customer {CustomerId} is not eligible to place an order; status {Status}", id, item.Status);
        return new(id, true, item.Status, canCart, canOrder, item.HasShipping, item.HasBilling);
    }

    public async Task<BatchCustomerEligibilityResponse> GetBatchEligibilityAsync(BatchCustomerEligibilityRequest request, CancellationToken cancellationToken)
    {
        if (request.CustomerIds.Count is < 1 or > 100) throw Validation("customerIds", "Provide between 1 and 100 customer IDs.");
        if (request.CustomerIds.Any(x => x == Guid.Empty)) throw Validation("customerIds", "customerIds cannot contain an empty GUID.");
        var ids = request.CustomerIds.Distinct().ToArray();
        var found = await customers.Query().Where(x => ids.Contains(x.Id)).Select(x => new
        {
            x.Id, x.Status,
            HasShipping = x.Addresses.Any(a => a.IsDefaultShipping),
            HasBilling = x.Addresses.Any(a => a.IsDefaultBilling)
        }).ToDictionaryAsync(x => x.Id, cancellationToken);
        var items = request.CustomerIds.Select(id => found.TryGetValue(id, out var x)
            ? new BatchCustomerEligibilityItemResponse(id, true, x.Status, x.Status is CustomerStatus.Active or CustomerStatus.Suspended, x.Status == CustomerStatus.Active, x.HasShipping, x.HasBilling)
            : new BatchCustomerEligibilityItemResponse(id, false, null, false, false, false, false)).ToList();
        return new(items);
    }

    private async Task ExecuteTransactionAsync(Func<Task> operation, CancellationToken cancellationToken)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await operation();
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task SaveCustomerAsync(CancellationToken cancellationToken)
    {
        try { await customers.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { throw new ConcurrencyConflictException("Customer data was modified by another request."); }
    }

    private void Touch(Customer customer)
    {
        customer.UpdatedAtUtc = timeProvider.GetUtcNow();
        customer.ConcurrencyToken = Guid.NewGuid();
    }

    internal static void EnsureConcurrency(Guid actual, Guid supplied, string message)
    {
        if (supplied == Guid.Empty || actual != supplied) throw new ConcurrencyConflictException(message);
    }

    internal static Common.Exceptions.ValidationException Validation(string field, string message) =>
        new("Validation failed.", new Dictionary<string, string[]> { [field] = [message] });

    private static void ValidateName(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 100) throw Validation(field, $"{field} is required and cannot exceed 100 characters.");
    }

    private static void ValidateQuery(CustomerQueryParameters query)
    {
        if (query.PageNumber < 1) throw Validation("pageNumber", "pageNumber must be at least 1.");
        if (query.PageSize is < 1 or > 100) throw Validation("pageSize", "pageSize must be between 1 and 100.");
        if (query.Status.HasValue && !Enum.IsDefined(query.Status.Value)) throw Validation("status", "The customer status is invalid.");
        var fields = new[] { "customerNumber", "firstName", "lastName", "email", "status", "createdAt", "updatedAt" };
        if (!fields.Contains(query.SortBy, StringComparer.OrdinalIgnoreCase)) throw Validation("sortBy", "The sort field is invalid.");
        if (!string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase) && !string.Equals(query.SortDirection, "desc", StringComparison.OrdinalIgnoreCase))
            throw Validation("sortDirection", "sortDirection must be asc or desc.");
        if (query.CreatedFromUtc.HasValue && query.CreatedToUtc.HasValue && query.CreatedFromUtc > query.CreatedToUtc)
            throw Validation("createdFromUtc", "createdFromUtc cannot be greater than createdToUtc.");
    }

    private static string GenerateCustomerNumber() => $"CUS-{Guid.NewGuid():N}"[..16].ToUpperInvariant();
    private static bool IsUniqueViolation(DbUpdateException exception, string column) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres
        && (postgres.ConstraintName?.Contains(column, StringComparison.OrdinalIgnoreCase) ?? false);

    internal static CustomerDetailsResponse MapDetails(Customer x)
    {
        var canCart = x.Status is CustomerStatus.Active or CustomerStatus.Suspended;
        var canOrder = x.Status == CustomerStatus.Active;
        return new(x.Id, x.CustomerNumber, x.FirstName, x.LastName, $"{x.FirstName} {x.LastName}", x.Email, x.PhoneNumber, x.Status,
            canCart, canOrder, x.CreatedAtUtc, x.UpdatedAtUtc, x.ConcurrencyToken,
            x.Addresses.OrderByDescending(a => a.IsDefaultShipping).ThenByDescending(a => a.IsDefaultBilling).ThenByDescending(a => a.CreatedAtUtc)
                .Select(AddressRules.Map).ToList());
    }
}

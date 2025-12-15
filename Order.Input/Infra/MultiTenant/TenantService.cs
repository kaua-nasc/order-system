namespace Order.Input.Infra.MultiTenant;

public class TenantService
{
    private readonly AsyncLocal<string?> _tenantId = new();

    public string? TenantId => _tenantId.Value;

    public void SetTenant(string tenantId) => _tenantId.Value = tenantId;
}
namespace DocsBrain.Core.Tenancy;

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}

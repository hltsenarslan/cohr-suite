// Comp.Api.Tests/CompTestSeed.cs

using Comp.Api.Domain;
using Comp.Api.Infrastructure;

public static class CompTestSeed
{
    public static void Run(CompDbContext db)
    {
        var t1 = "a0cb8251-16bc-6bde-cc66-5d76b0c7b0ac";
        var t2 = "44709835-d55a-ef2a-2327-5fdca19e55d8";

        db.Salaries.AddRange(
            new Salary { TenantId = t1, Employee = "e1", Amount = 1000m, EffectiveDate = DateTime.Now.AddMonths(3) },
            new Salary { TenantId = t2, Employee = "e2", Amount = 2000m, EffectiveDate = DateTime.Now.AddMonths(3)}
        );

        db.SaveChanges();
    }
}
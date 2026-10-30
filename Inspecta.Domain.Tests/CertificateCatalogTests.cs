using Inspecta.Application.Certificates;

namespace Inspecta.Domain.Tests;

/// <summary>The certificate templates and the machine-to-template mapping taken from the 60 source forms.</summary>
public sealed class CertificateCatalogTests
{
    [Fact]
    public void ThereAreFifteenTemplatesAndFiftyEightKnownMachines()
    {
        Assert.Equal(15, CertificateTemplateCatalog.Templates.Count);
        Assert.Equal(58, CertificateTemplateCatalog.Items.Count);
        Assert.Equal(14, CertificateTemplateCatalog.Templates.Count(template => template.Checklist.Count > 0));
        Assert.Empty(CertificateTemplateCatalog.Basic.Checklist);
    }

    [Theory]
    [InlineData("Forklift Truck", "forklift")]
    [InlineData("Forklift", "forklift")]
    [InlineData("  forklift   TRUCK ", "forklift")]
    [InlineData("Gantry Crane", "girder_crane")]
    [InlineData("Jib Crane", "girder_crane")]
    [InlineData("Over head crane", "girder_crane")]
    [InlineData("Tower Crane", "tower_crane")]
    [InlineData("Manlift", "manlift")]
    [InlineData("Scissor Lift", "basic")]
    [InlineData("Chain Block", "basic")]
    [InlineData("Man Basket", "basic")]
    [InlineData("Conveyor Belt System", "conveyor_belt")]
    [InlineData("Diesel Generator", "diesel_generator")]
    [InlineData("Elevator", "elevator")]
    [InlineData("RTG", "rtg")]
    [InlineData("Piling Drilling machine", "basic")]
    [InlineData("Wheel Loader With Fork Arm", "wheel_loader_fork")]
    [InlineData("Some unknown machine", "basic")]
    public void ANameIsMappedToItsTemplate(string name, string expected) =>
        Assert.Equal(expected, CertificateTemplateCatalog.Resolve(name).Template.Key);

    [Fact]
    public void EveryKnownNameResolvesToItsOwnTemplate()
    {
        foreach (var item in CertificateTemplateCatalog.Items)
        {
            foreach (var name in item.Aliases.Prepend(item.Name))
            {
                var match = CertificateTemplateCatalog.Resolve(name);
                Assert.Equal(item.Template, match.Template.Key);
                Assert.Equal(item, match.Item);
            }
        }
    }

    [Fact]
    public void TheFirstKnownNameWinsAndBlankNamesAreSkipped()
    {
        var match = CertificateTemplateCatalog.Resolve(null, " ", "Unknown thing", "Tower Crane", "Manlift");

        Assert.Equal("tower_crane", match.Template.Key);
        Assert.Equal("Tower Crane", match.Item!.Name);
        Assert.Null(CertificateTemplateCatalog.Resolve("Unknown thing").Item);
    }

    [Fact]
    public void SomeMachinesHaveTwoStandardsAndSomeNone()
    {
        string[] Standards(string name) => CertificateTemplateCatalog.Resolve(name).Item!.Standards.ToArray();

        Assert.Equal(2, Standards("Forklift Truck").Length);
        Assert.Equal(2, Standards("Rubber Tired Gantry Crane (RTG)").Length);
        Assert.Equal(2, Standards("Terminal tractor").Length);
        Assert.Empty(Standards("Container"));
        Assert.Empty(Standards("Trailer Truck"));
        Assert.Equal(["BS EN 13157:2004 +A1:2009"], Standards("Snatch Block"));
    }

    [Fact]
    public void TheSameFormSharedByGantryJibAndOverheadHasOneChecklist()
    {
        var gantry = CertificateTemplateCatalog.Resolve("Gantry Crane").Template;

        Assert.Same(gantry, CertificateTemplateCatalog.Resolve("Jib Crane").Template);
        Assert.Same(gantry, CertificateTemplateCatalog.Resolve("Overhead Crane").Template);
    }

    [Fact]
    public void TemplateKeysAreUniqueInsideEachTemplate()
    {
        foreach (var template in CertificateTemplateCatalog.Templates)
        {
            Assert.Equal(template.Fields.Count, template.Fields.Select(field => field.Key).Distinct().Count());
            Assert.Equal(template.Checklist.Count, template.Checklist.Select(section => section.Key).Distinct().Count());
            var paths = template.ChecklistPaths.ToArray();
            Assert.Equal(paths.Length, paths.Distinct().Count());
            Assert.All(template.Checklist, section => Assert.NotEmpty(section.Items));
        }
    }

    [Fact]
    public void TheTechnicalFieldsFollowTheSourceForms()
    {
        string[] Keys(string name) => CertificateTemplateCatalog.Resolve(name).Template.Fields.Select(field => field.Key).ToArray();

        Assert.Equal(["description", "swl", "quantity", "serialNumber", "manufacturer"], Keys("Chain Block"));
        Assert.Contains("spanLength", Keys("Jib Crane"));
        Assert.Contains("boomLength", Keys("Tower Crane"));
        Assert.Contains("maxPlatformHeight", Keys("Manlift"));
        Assert.Contains("ndt", Keys("Forklift"));
    }
}

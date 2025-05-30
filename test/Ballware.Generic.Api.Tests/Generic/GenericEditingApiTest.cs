using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Ballware.Generic.Api.Endpoints;
using Ballware.Generic.Api.Tests.Utils;
using Ballware.Generic.Authorization;
using Ballware.Generic.Data.Public;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Quartz;
using JsonSerializer = System.Text.Json.JsonSerializer;
using RemoveResult = Ballware.Generic.Tenant.Data.RemoveResult;

namespace Ballware.Generic.Api.Tests.Generic;

public class FakeEntity : IEditable
{
    public Guid Id { get; set; }
    public string? Name { get; set; }
}

[TestFixture]
public class GenericEditingApiTest : ApiMappingBaseTest
{
    private string ExpectedApplication { get; } = "test";
    private string ExpectedEntity { get; } = "fakeentity";
    
    private Mock<ISchedulerFactory> SchedulerFactoryMock { get; set; } = null!;
    private Mock<IGenericFileStorageAdapter> StorageAdapterMock { get; set; } = null!;
    private Mock<IPrincipalUtils> PrincipalUtilsMock { get; set; } = null!;
    private Mock<ITenantRightsChecker> TenantRightsCheckerMock { get; set; } = null!;
    private Mock<IEntityRightsChecker> EntityRightsCheckerMock { get; set; } = null!;
    
    private Mock<IMetadataAdapter> MetadataAdapterMock { get; set; } = null!;
    private Mock<ITenantGenericProvider> TenantGenericProviderMock { get; set; } = null!;
    
    private HttpClient Client { get; set; } = null!;
    
    [SetUp]
    public override async Task SetUp()
    {
        await base.SetUp();
        
        SchedulerFactoryMock = new Mock<ISchedulerFactory>();
        StorageAdapterMock = new Mock<IGenericFileStorageAdapter>();
        PrincipalUtilsMock = new Mock<IPrincipalUtils>();
        TenantRightsCheckerMock = new Mock<ITenantRightsChecker>();
        EntityRightsCheckerMock = new Mock<IEntityRightsChecker>();
        MetadataAdapterMock = new Mock<IMetadataAdapter>();
        TenantGenericProviderMock = new Mock<ITenantGenericProvider>();
        
        Client = await CreateApplicationClientAsync("genericApi", services =>
        {
            services.AddSingleton(SchedulerFactoryMock.Object);
            services.AddSingleton(StorageAdapterMock.Object);
            services.AddSingleton(PrincipalUtilsMock.Object);
            services.AddSingleton(TenantRightsCheckerMock.Object);
            services.AddSingleton(EntityRightsCheckerMock.Object);
            services.AddSingleton(MetadataAdapterMock.Object);
            services.AddSingleton(TenantGenericProviderMock.Object);
        }, app =>
        {
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGenericDataApi("generic");
            });
        });
    }
    
    [Test]
    public async Task HandleAll_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };

        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };

        var fakeEntity = new Entity()
        {
            Application = ExpectedApplication,
            Identifier = ExpectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, ExpectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, ExpectedApplication, ExpectedEntity,
                It.IsAny<IDictionary<string, object>>(), "view"))
            .ReturnsAsync(true);

        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(),
                It.IsAny<IDictionary<string, object>>(), "view", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(p => p.AllAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedList);
        
        // Act
        var response = await Client.GetAsync($"generic/{ExpectedApplication}/{ExpectedEntity}/all?identifier=primary");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        var result = JsonSerializer.Deserialize<IEnumerable<FakeEntity>>(await response.Content.ReadAsStringAsync());

        Assert.That(DeepComparer.AreListsEqual(expectedList, result, TestContext.WriteLine));
    }
    
    [Test]
    public async Task HandleAll_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());

        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "view"))
            .ReturnsAsync(true);
        
        // Act
        var notFoundResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/all?identifier=primary");
        
        // Assert
        Assert.That(notFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleAll_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);

        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "view"))
            .ReturnsAsync(false);
        
        // Act
        var unauthorizedResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/all?identifier=primary");
        
        // Assert
        Assert.That(unauthorizedResponse.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleNew_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "add"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(),
                It.IsAny<IDictionary<string, object>>(), "add", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(p => p.NewAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedEntry);
        
        // Act
        var response = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/new?identifier=primary");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        var result = JsonSerializer.Deserialize<FakeEntity>(await response.Content.ReadAsStringAsync());

        Assert.That(DeepComparer.AreEqual(expectedEntry, result, TestContext.WriteLine));
    }
    
    [Test]
    public async Task HandleNew_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());

        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "add"))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(p => p.NewAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedEntry);
        
        // Act
        var notFoundResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/new?identifier=primary");
        
        // Assert
        Assert.That(notFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleNew_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "add"))
            .ReturnsAsync(false);

        TenantGenericProviderMock
            .Setup(p => p.NewAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedEntry);
        
        // Act
        var unauthorizedResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/new?identifier=primary");
        
        // Assert
        Assert.That(unauthorizedResponse.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleById_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };

        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<IDictionary<string, object>>(), "view"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(),
                It.IsAny<IDictionary<string, object>>(), "view", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(p => p.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(expectedEntry);
        
        // Act
        var response = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/byid?identifier=primary&id={expectedEntry.Id}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        var result = JsonSerializer.Deserialize<FakeEntity>(await response.Content.ReadAsStringAsync());

        Assert.That(DeepComparer.AreEqual(expectedEntry, result, TestContext.WriteLine));
    }
    
    [Test]
    public async Task HandleById_tenant_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };

        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "view"))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(p => p.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(expectedEntry);
        
        // Act
        var tenantNotFoundResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/byid?identifier=primary&id={expectedEntry.Id}");
        
        // Assert
        Assert.That(tenantNotFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleById_record_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };

        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "view"))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(p => p.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(null as FakeEntity);
        
        // Act
        var recordNotFoundResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/byid?identifier=primary&id={Guid.NewGuid()}");
        
        // Assert
        Assert.That(recordNotFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleById_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };

        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "view"))
            .ReturnsAsync(false);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(),
                It.IsAny<IDictionary<string, object>>(), "view", It.IsAny<object>(), false))
            .ReturnsAsync(false);
        
        TenantGenericProviderMock
            .Setup(p => p.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(expectedEntry);
        
        // Act
        var unauthorizedResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/byid?identifier=primary&id={expectedEntry.Id}");
        
        // Assert
        Assert.That(unauthorizedResponse.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleSave_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "edit"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(),
                It.IsAny<IDictionary<string, object>>(), "edit", It.IsAny<object>(), true))
            .ReturnsAsync(true);

        TenantGenericProviderMock
            .Setup(r => r.SaveAsync(fakeTenant, fakeEntity, expectedUserId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo("primary"));
                    Assert.That(entry, Is.Not.Null);
                    Assert.That(entry["Id"], Is.EqualTo(expectedEntry.Id.ToString()));
                    Assert.That(entry["Name"], Is.EqualTo(expectedEntry.Name));    
                });
            });
        
        // Act
        var response = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/save?identifier=primary", JsonContent.Create(expectedEntry, null, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = null
        }));
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        TenantGenericProviderMock.Verify(r => r.SaveAsync(
            fakeTenant, fakeEntity, expectedUserId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()), Times.Once);
    }
    
    [Test]
    public async Task HandleSave_tenant_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "edit"))
            .ReturnsAsync(true);

        TenantGenericProviderMock
            .Setup(r => r.SaveAsync(fakeTenant, fakeEntity, expectedUserId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo("primary"));
                    Assert.That(entry, Is.Not.Null);
                    Assert.That(entry["Id"], Is.EqualTo(expectedEntry.Id));
                    Assert.That(entry["Name"], Is.EqualTo(expectedEntry.Name));    
                });
            });
        
        // Act
        var tenantNotFoundResponse = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/save?identifier=primary", JsonContent.Create(expectedEntry, null, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = null
        }));
        
        // Assert
        Assert.That(tenantNotFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleSave_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "edit"))
            .ReturnsAsync(false);

        TenantGenericProviderMock
            .Setup(r => r.SaveAsync(fakeTenant, fakeEntity, expectedUserId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo("primary"));
                    Assert.That(entry, Is.Not.Null);
                    Assert.That(entry["Id"], Is.EqualTo(expectedEntry.Id));
                    Assert.That(entry["Name"], Is.EqualTo(expectedEntry.Name));    
                });
            });
        
        // Act
        var unauthorizedResponse = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/save?identifier=primary", JsonContent.Create(expectedEntry, null, new JsonSerializerOptions()
        {
            PropertyNamingPolicy = null
        }));
        
        // Assert
        Assert.That(unauthorizedResponse.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleSave_body_empty()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "edit"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(),
                It.IsAny<IDictionary<string, object>>(), "edit", It.IsAny<object>(), true))
            .ReturnsAsync(true);

        TenantGenericProviderMock
            .Setup(r => r.SaveAsync(fakeTenant, fakeEntity, expectedUserId, "primary", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, IDictionary<string, object> entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo("primary"));
                    Assert.That(entry, Is.Not.Null);
                    Assert.That(entry["Id"], Is.EqualTo(expectedEntry.Id.ToString()));
                    Assert.That(entry["Name"], Is.EqualTo(expectedEntry.Name));    
                });
            });
        
        // Act
        var response = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/save?identifier=primary", new StringContent(string.Empty));
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.BadRequest));
    }
    
    [Test]
    public async Task HandleRemove_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "delete"))
            .ReturnsAsync(true);

        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(), It.IsAny<IDictionary<string, object>>(), 
                "delete", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(r => r.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(expectedEntry);
        
        TenantGenericProviderMock
            .Setup(r => r.RemoveAsync(fakeTenant, fakeEntity, expectedUserId, It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(new RemoveResult()
            {
                Result = true
            })
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, Guid id) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(id, Is.EqualTo(expectedEntry.Id));
                });
            });
        
        // Act
        var response = await Client.DeleteAsync($"generic/{expectedApplication}/{expectedEntity}/remove/{expectedEntry.Id}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        TenantGenericProviderMock.Verify(r => r.RemoveAsync(
            fakeTenant, fakeEntity, expectedUserId, It.IsAny<IDictionary<string, object>>(), expectedEntry.Id), Times.Once);
    }
    
    [Test]
    public async Task HandleRemove_tenant_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "delete"))
            .ReturnsAsync(true);

        TenantGenericProviderMock
            .Setup(r => r.RemoveAsync(fakeTenant, fakeEntity, expectedUserId, It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(new RemoveResult()
            {
                Result = true
            })
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, Guid id) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(id, Is.EqualTo(expectedEntry.Id));
                });
            });
        
        // Act
        var tenantNotFoundResponse = await Client.DeleteAsync($"generic/{expectedApplication}/{expectedEntity}/remove/{expectedEntry.Id}");
        
        // Assert
        Assert.That(tenantNotFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleRemove_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "delete"))
            .ReturnsAsync(false);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(), It.IsAny<IDictionary<string, object>>(), 
                "delete", It.IsAny<object>(), false))
            .ReturnsAsync(false);
        
        TenantGenericProviderMock
            .Setup(r => r.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(expectedEntry);

        TenantGenericProviderMock
            .Setup(r => r.RemoveAsync(fakeTenant, fakeEntity, expectedUserId, It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(new RemoveResult()
            {
                Result = true
            })
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, Guid id) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(id, Is.EqualTo(expectedEntry.Id));
                });
            });
        
        // Act
        var unauthorizedResponse = await Client.DeleteAsync($"generic/{expectedApplication}/{expectedEntity}/remove/{expectedEntry.Id}");
        
        // Assert
        Assert.That(unauthorizedResponse.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleRemove_preliminary_check_declined()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedEntry = new FakeEntity()
        {
            Id = Guid.NewGuid(),
            Name = "Name 1"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);

        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "delete"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(), It.IsAny<IDictionary<string, object>>(), 
                "delete", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(r => r.ByIdAsync<dynamic>(fakeTenant, fakeEntity, "primary", It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(expectedEntry);
        
        TenantGenericProviderMock
            .Setup(r => r.RemoveAsync(fakeTenant, fakeEntity, expectedUserId, It.IsAny<IDictionary<string, object>>(), expectedEntry.Id))
            .ReturnsAsync(new RemoveResult()
            {
                Messages = ["An error occurred while trying to remove the entry."],
                Result = false
            })
            .Callback((Metadata.Tenant tenant, Entity entity, Guid? userId, IDictionary<string, object> claims, Guid id) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenant, Is.EqualTo(fakeTenant));
                    Assert.That(entity, Is.EqualTo(fakeEntity));
                    Assert.That(claims, Is.EqualTo(Claims));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(id, Is.EqualTo(expectedEntry.Id));
                });
            });
        
        // Act
        var badRequestResponse = await Client.DeleteAsync($"generic/{expectedApplication}/{expectedEntity}/remove/{expectedEntry.Id}");
        
        // Assert
        Assert.That(badRequestResponse.StatusCode,Is.EqualTo(HttpStatusCode.BadRequest));
    }
    
    [Test]
    public async Task HandleExport_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };

        var expectedResult = new GenericExport()
        {
            FileName = "export.json",
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedList)),
            MediaType = "application/json"
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "export"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(), It.IsAny<IDictionary<string, object>>(), 
                "export", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(r => r.ExportAsync(fakeTenant, fakeEntity, "export", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var response = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/export?identifier=export{string.Join("", expectedList.Select(c => $"&Id={c.Id}"))}");
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        var result = JsonSerializer.Deserialize<IEnumerable<FakeEntity>>(await response.Content.ReadAsStringAsync());

        Assert.That(DeepComparer.AreListsEqual(expectedList, result, TestContext.WriteLine));
    }
    
    [Test]
    public async Task HandleExport_tenant_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };

        var expectedResult = new GenericExport()
        {
            FileName = "export.json",
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedList)),
            MediaType = "application/json"
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "export"))
            .ReturnsAsync(true);
        
        TenantGenericProviderMock
            .Setup(r => r.ExportAsync(fakeTenant, fakeEntity, "export", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var notFoundResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/export?identifier=export");
        
        // Assert
        Assert.That(notFoundResponse.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleExport_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };

        var expectedResult = new GenericExport()
        {
            FileName = "export.json",
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedList)),
            MediaType = "application/json"
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "export"))
            .ReturnsAsync(false);
        
        TenantGenericProviderMock
            .Setup(r => r.ExportAsync(fakeTenant, fakeEntity, "export", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var unauthorizedResponse = await Client.GetAsync($"generic/{expectedApplication}/{expectedEntity}/export?identifier=export");
        
        // Assert
        Assert.That(unauthorizedResponse.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleExportToUrl_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";
        var expectedExportId = Guid.NewGuid();

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };

        var expectedResult = new GenericExport()
        {
            FileName = "export.json",
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedList)),
            MediaType = "application/json"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "export"))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, It.IsAny<Entity>(), It.IsAny<IDictionary<string, object>>(), 
                "export", It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        MetadataAdapterMock
            .Setup(r => r.CreateExportForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<ExportCreatePayload>()))
            .ReturnsAsync(expectedExportId)
            .Callback((Guid tenantId, Guid userId, ExportCreatePayload entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(entry.Application, Is.EqualTo(expectedApplication));
                    Assert.That(entry.Entity, Is.EqualTo(expectedEntity));
                    Assert.That(entry.Query, Is.EqualTo("export"));
                    Assert.That(entry.MediaType, Is.EqualTo("application/json"));
                });
            });
        
        TenantGenericProviderMock
            .Setup(r => r.ExportAsync(fakeTenant, fakeEntity, "export", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var response = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/exporturl?identifier=export", new FormUrlEncodedContent(
            expectedList.Select(item => new KeyValuePair<string, string>("Id", item.Id.ToString()))));
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.OK));
        
        Assert.That(Guid.TryParse(await response.Content.ReadAsStringAsync(), out Guid result), Is.True);
        
        Assert.That(result, Is.EqualTo(expectedExportId));

        MetadataAdapterMock
            .Verify(r => r.CreateExportForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<ExportCreatePayload>()), 
                Times.Once);
    }
    
    [Test]
    public async Task HandleExportToUrl_tenant_not_found()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";
        var expectedExportId = Guid.NewGuid();

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };

        var expectedResult = new GenericExport()
        {
            FileName = "export.json",
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedList)),
            MediaType = "application/json"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(Guid.NewGuid());
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "export"))
            .ReturnsAsync(true);

        MetadataAdapterMock
            .Setup(r => r.CreateExportForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<ExportCreatePayload>()))
            .ReturnsAsync(expectedExportId)
            .Callback((Guid tenantId, Guid userId, ExportCreatePayload entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(entry.Application, Is.EqualTo(expectedApplication));
                    Assert.That(entry.Entity, Is.EqualTo(expectedEntity));
                    Assert.That(entry.Query, Is.EqualTo("export"));
                    Assert.That(entry.MediaType, Is.EqualTo("application/json"));
                });
            });
        
        TenantGenericProviderMock
            .Setup(r => r.ExportAsync(fakeTenant, fakeEntity, "export", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var response = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/exporturl?identifier=export", new FormUrlEncodedContent(
            expectedList.Select(item => new KeyValuePair<string, string>("Id", item.Id.ToString()))));
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.NotFound));
    }
    
    [Test]
    public async Task HandleExportToUrl_not_authorized()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";
        var expectedExportId = Guid.NewGuid();

        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };

        var expectedResult = new GenericExport()
        {
            FileName = "export.json",
            Data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(expectedList)),
            MediaType = "application/json"
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "export"))
            .ReturnsAsync(false);

        MetadataAdapterMock
            .Setup(r => r.CreateExportForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<ExportCreatePayload>()))
            .ReturnsAsync(expectedExportId)
            .Callback((Guid tenantId, Guid userId, ExportCreatePayload entry) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(tenantId, Is.EqualTo(expectedTenantId));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(entry.Application, Is.EqualTo(expectedApplication));
                    Assert.That(entry.Entity, Is.EqualTo(expectedEntity));
                    Assert.That(entry.Query, Is.EqualTo("export"));
                    Assert.That(entry.MediaType, Is.EqualTo("application/json"));
                });
            });
        
        TenantGenericProviderMock
            .Setup(r => r.ExportAsync(fakeTenant, fakeEntity, "export", It.IsAny<IDictionary<string, object>>(), It.IsAny<IDictionary<string, object>>()))
            .ReturnsAsync(expectedResult);
        
        // Act
        var response = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/exporturl?identifier=export", new FormUrlEncodedContent(
            expectedList.Select(item => new KeyValuePair<string, string>("Id", item.Id.ToString()))));
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.Unauthorized));
    }
    
    [Test]
    public async Task HandleImport_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedApplication = "test";
        var expectedEntity = "fakeentity";
        var expectedJobId = Guid.NewGuid();
        
        var expectedList = new List<FakeEntity>()
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 1"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 2"
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Name 3"
            }
        };
        
        var fakeTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };
        
        var fakeEntity = new Entity()
        {
            Application = expectedApplication,
            Identifier = expectedEntity
        };
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserTenandId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedTenantId);
        
        PrincipalUtilsMock
            .Setup(p => p.GetUserId(It.IsAny<ClaimsPrincipal>()))
            .Returns(expectedUserId);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(fakeTenant);
        
        MetadataAdapterMock
            .Setup(m => m.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, expectedEntity))
            .ReturnsAsync(fakeEntity);
        
        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(fakeTenant, expectedApplication, expectedEntity,
                It.IsAny<Dictionary<string, object>>(), "import"))
            .ReturnsAsync(true);

        StorageAdapterMock
            .Setup(s => s.UploadFileForOwnerAsync(expectedUserId.ToString(), "import.json", "application/json",
                It.IsAny<Stream>()))
            .Callback((string owner, string fileName, string mediaType, Stream stream) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(owner, Is.EqualTo(expectedUserId.ToString()));
                    Assert.That(fileName, Is.EqualTo("import.json"));
                    Assert.That(mediaType, Is.EqualTo("application/json"));
                    using var reader = new StreamReader(stream);
                    var content = reader.ReadToEnd();
                    var importedList = JsonSerializer.Deserialize<List<FakeEntity>>(content);
                    Assert.That(DeepComparer.AreListsEqual(expectedList, importedList, TestContext.WriteLine));
                });
            });

        MetadataAdapterMock
            .Setup(r => r.CreateJobForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<JobCreatePayload>()))
            .ReturnsAsync(expectedJobId);
        
        var schedulerMock = new Mock<IScheduler>();

        schedulerMock
            .Setup(s => s.TriggerJob(It.IsAny<JobKey>(), It.IsAny<JobDataMap>(), It.IsAny<CancellationToken>()))
            .Callback((JobKey jobKey, JobDataMap jobData, CancellationToken cancellationToken) =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(jobKey, Is.EqualTo(JobKey.Create("import", "generic")));
                    Assert.That(cancellationToken, Is.EqualTo(CancellationToken.None));
                    Assert.That(jobData, Is.Not.Null);
                });
            });
        
        SchedulerFactoryMock
            .Setup(s => s.GetScheduler(CancellationToken.None))
            .ReturnsAsync(schedulerMock.Object);
        
        // Act
        var payload = new MultipartFormDataContent();
        
        payload.Add(new StreamContent(new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(expectedList)))), "files", "import.json");
        
        var response = await Client.PostAsync($"generic/{expectedApplication}/{expectedEntity}/import?identifier=import", payload);
        
        // Assert
        Assert.That(response.StatusCode,Is.EqualTo(HttpStatusCode.Created));
    }
}
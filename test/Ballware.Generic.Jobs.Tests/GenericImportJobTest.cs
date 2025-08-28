using System.Dynamic;
using Ballware.Shared.Authorization;
using Ballware.Generic.Jobs.Internal;
using Ballware.Generic.Metadata;
using Ballware.Generic.Tenant.Data;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Newtonsoft.Json;
using Quartz;

namespace Ballware.Generic.Jobs.Tests;

public class FakeEntity
{
    public string Name { get; set; } = "TestEntity";
}

[TestFixture]
public class TenantableMetaImportJobTest
{
    private const string ExpectedFunctionIdentifier = "importjson";
    
    private Mock<ITenantGenericProvider> GenericProviderMock { get; set; }
    private Mock<ITenantRightsChecker> TenantRightsCheckerMock { get; set; }
    private Mock<IEntityRightsChecker> EntityRightsCheckerMock { get; set; }
    private Mock<IMetadataAdapter> MetadataAdapterMock { get; set; }
    
    private Mock<IJobsFileStorageAdapter> JobsFileStorageAdapterMock { get; set; }
    private Mock<IJobExecutionContext> JobExecutionContextMock { get; set; }

    private ServiceProvider ServiceProvider { get; set; }
    
    [SetUp]
    public void Setup()
    {
        GenericProviderMock = new Mock<ITenantGenericProvider>();
        TenantRightsCheckerMock = new Mock<ITenantRightsChecker>();
        EntityRightsCheckerMock = new Mock<IEntityRightsChecker>();
        MetadataAdapterMock = new Mock<IMetadataAdapter>();
        
        JobsFileStorageAdapterMock = new Mock<IJobsFileStorageAdapter>();
        
        JobExecutionContextMock = new Mock<IJobExecutionContext>();
        
        var triggerMock = new Mock<ITrigger>();
        
        triggerMock
            .Setup(trigger => trigger.JobKey)
            .Returns(GenericImportJob.Key);
        
        JobExecutionContextMock
            .Setup(c => c.Trigger)
            .Returns(triggerMock.Object);
        
        var serviceCollection = new ServiceCollection();
        
        serviceCollection.AddSingleton(GenericProviderMock.Object);
        serviceCollection.AddSingleton(TenantRightsCheckerMock.Object);
        serviceCollection.AddSingleton(EntityRightsCheckerMock.Object);
        serviceCollection.AddSingleton(MetadataAdapterMock.Object);
        
        ServiceProvider = serviceCollection
            .BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        ServiceProvider.Dispose();
    }
    
    [Test]
    public async Task Execute_succeeds()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedJobId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedClaims = new Dictionary<string, object>()
        {
            { "sub", expectedUserId.ToString() },
            { "right", new [] { "add", "edit", "view", "delete" } }
        };
        
        var expectedTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };

        var expectedEntity = new Metadata.Entity()
        {
            Application = "fakeapplication",
            Identifier = "fakeentity"
        };

        var expectedEntry = new Dictionary<string, object>()
        {
            { "Id", Guid.NewGuid() }
        };
        
        var expectedFileStream = new MemoryStream();
        
        MetadataAdapterMock
            .Setup(r => r.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(expectedTenant);
        
        MetadataAdapterMock
            .Setup(r => r.MetadataForEntityByTenantAndIdentifierAsync(expectedTenantId, "fakeentity"))
            .ReturnsAsync(expectedEntity);
        
        JobsFileStorageAdapterMock
            .Setup(s => s.TemporaryFileByIdAsync(expectedTenantId, expectedTemporaryId))
            .ReturnsAsync(expectedFileStream);
        
        GenericProviderMock
            .Setup(r => r.ImportAsync(
                expectedTenant,
                expectedEntity,
                ExpectedFunctionIdentifier,
                It.IsAny<Guid>(),
                It.IsAny<IDictionary<string, object>>(),
                expectedFileStream,
                It.IsAny<Func<IDictionary<string, object>, Task<bool>>>()))
            .Returns(async (Metadata.Tenant tenant, Metadata.Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, Stream stream,
                Func<IDictionary<string, object>, Task<bool>> authorize) =>
            {
                await Assert.MultipleAsync(async () =>
                {
                    Assert.That(tenant, Is.EqualTo(expectedTenant));
                    Assert.That(entity, Is.EqualTo(expectedEntity));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo(ExpectedFunctionIdentifier));
                    Assert.That(claims, Is.EqualTo(expectedClaims));
                    Assert.That(stream, Is.EqualTo(expectedFileStream));
                    Assert.That(await authorize(expectedEntry), Is.True);
                });
            });

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenant, "fakeapplication", "fakeentity", It.IsAny<IDictionary<string, object>>(), ExpectedFunctionIdentifier))
            .ReturnsAsync(true);

        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, expectedEntity, It.IsAny<IDictionary<string, object>>(),
                ExpectedFunctionIdentifier, It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        var jobDataMap = new JobDataMap
        {
            { "tenantId", expectedTenantId },
            { "application", "fakeapplication" },
            { "entity", "fakeentity" },
            { "jobId", expectedJobId },
            { "userId", expectedUserId },
            { "identifier", ExpectedFunctionIdentifier },
            { "claims", JsonConvert.SerializeObject(expectedClaims) },
            { "file", expectedTemporaryId }
        };

        JobExecutionContextMock
            .Setup(c => c.MergedJobDataMap)
            .Returns(jobDataMap);
        
        var job = new GenericImportJob(
            GenericProviderMock.Object,
            TenantRightsCheckerMock.Object,
            EntityRightsCheckerMock.Object,
            MetadataAdapterMock.Object,
            JobsFileStorageAdapterMock.Object);
        
        // Act
        await job.Execute(JobExecutionContextMock.Object);
        
        // Assert
        MetadataAdapterMock.Verify(
            r => r.UpdateJobForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<JobUpdatePayload>()),
            Times.Exactly(2));
        
        JobsFileStorageAdapterMock.Verify(s => s.TemporaryFileByIdAsync(expectedTenantId, expectedTemporaryId), Times.Once);
        JobsFileStorageAdapterMock.Verify(s => s.RemoveTemporaryFileByIdBehalfOfUserAsync(expectedTenantId, expectedUserId, expectedTemporaryId), Times.Once);
        
        GenericProviderMock.Verify(r => r.ImportAsync(
            expectedTenant,
            expectedEntity,
            ExpectedFunctionIdentifier,
            expectedUserId,
            It.IsAny<IDictionary<string, object>>(),
            expectedFileStream,
            It.IsAny<Func<IDictionary<string, object>, Task<bool>>>()), Times.Once);
    }
    
    [Test]
    public void Execute_failed_unknown_tenant()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedJobId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedClaims = new Dictionary<string, object>()
        {
            { "sub", expectedUserId.ToString() }
        };
        
        var expectedTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };

        var expectedEntity = new Metadata.Entity()
        {
            Application = "fakeapplication",
            Identifier = "fakeentity"
        };

        var expectedEntry = new Dictionary<string, object>()
        {
            { "Id", Guid.NewGuid() }
        };
        
        var expectedFileStream = new MemoryStream();
        
        MetadataAdapterMock
            .Setup(r => r.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(null as Metadata.Tenant);
        
        JobsFileStorageAdapterMock
            .Setup(s => s.TemporaryFileByIdAsync(expectedTenantId, expectedTemporaryId))
            .ReturnsAsync(expectedFileStream);
        
        GenericProviderMock
            .Setup(r => r.ImportAsync(
                expectedTenant,
                expectedEntity,
                "importjson",
                expectedUserId,
                expectedClaims,
                expectedFileStream,
                It.IsAny<Func<IDictionary<string, object>, Task<bool>>>()))
            .Returns(async (Metadata.Tenant tenant, Metadata.Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, Stream stream,
                Func<IDictionary<string, object>, Task<bool>> authorize) =>
            {
                await Assert.MultipleAsync(async () =>
                {
                    Assert.That(tenant, Is.EqualTo(expectedTenant));
                    Assert.That(entity, Is.EqualTo(expectedEntity));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo(ExpectedFunctionIdentifier));
                    Assert.That(claims, Is.EqualTo(expectedClaims));
                    Assert.That(stream, Is.EqualTo(expectedFileStream));
                    Assert.That(await authorize(expectedEntry), Is.True);
                });
            });

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenant, "fakeapplication", "fakeentity", expectedClaims, ExpectedFunctionIdentifier))
            .ReturnsAsync(true);
        
        EntityRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenantId, expectedEntity, It.IsAny<IDictionary<string, object>>(),
                ExpectedFunctionIdentifier, It.IsAny<object>(), true))
            .ReturnsAsync(true);
        
        var jobDataMap = new JobDataMap
        {
            { "tenantId", expectedTenantId },
            { "jobId", expectedJobId },
            { "userId", expectedUserId },
            { "application", "fakeapplication" },
            { "entity", "fakeentity" },            
            { "identifier", ExpectedFunctionIdentifier },
            { "claims", JsonConvert.SerializeObject(expectedClaims) },
            { "file", expectedTemporaryId }
        };

        JobExecutionContextMock
            .Setup(c => c.MergedJobDataMap)
            .Returns(jobDataMap);
        
        var job = new GenericImportJob(
            GenericProviderMock.Object,
            TenantRightsCheckerMock.Object,
            EntityRightsCheckerMock.Object,
            MetadataAdapterMock.Object,
            JobsFileStorageAdapterMock.Object);
        
        // Act
        Assert.ThrowsAsync<JobExecutionException>(async () => await job.Execute(JobExecutionContextMock.Object), $"Tenant {expectedTenantId} unknown");
        
        // Assert
        MetadataAdapterMock.Verify(
            r => r.UpdateJobForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<JobUpdatePayload>()),
            Times.Exactly(2));
        
        JobsFileStorageAdapterMock.Verify(s => s.TemporaryFileByIdAsync(expectedTenantId, expectedTemporaryId), Times.Never);
        JobsFileStorageAdapterMock.Verify(s => s.RemoveTemporaryFileByIdBehalfOfUserAsync(expectedTenantId, expectedUserId, expectedTemporaryId), Times.Never);
        
        GenericProviderMock.Verify(r => r.ImportAsync(
            expectedTenant,
            expectedEntity,
            ExpectedFunctionIdentifier,
            expectedUserId,
            expectedClaims,
            expectedFileStream,
            It.IsAny<Func<dynamic, Task<bool>>>()), Times.Never);
    }
    
    [Test]
    public void Execute_failed_mandatory_parameter_missing()
    {
        // Arrange
        var expectedTenantId = Guid.NewGuid();
        var expectedTemporaryId = Guid.NewGuid();
        var expectedJobId = Guid.NewGuid();
        var expectedUserId = Guid.NewGuid();
        var expectedClaims = new Dictionary<string, object>();
        
        var expectedTenant = new Metadata.Tenant()
        {
            Id = expectedTenantId,
            Provider = "mssql"
        };

        var expectedEntity = new Metadata.Entity()
        {
            Application = "fakeapplication",
            Identifier = "fakeentity"
        };

        var expectedEntry = new Dictionary<string, object>()
        {
            { "Id", Guid.NewGuid() }
        };
        
        var expectedFileStream = new MemoryStream();
        
        MetadataAdapterMock
            .Setup(r => r.MetadataForTenantByIdAsync(expectedTenantId))
            .ReturnsAsync(null as Metadata.Tenant);
        
        JobsFileStorageAdapterMock
            .Setup(s => s.TemporaryFileByIdAsync(expectedTenantId, expectedTemporaryId))
            .ReturnsAsync(expectedFileStream);
        
        GenericProviderMock
            .Setup(r => r.ImportAsync(
                expectedTenant,
                expectedEntity,
                "importjson",
                expectedUserId,
                expectedClaims,
                expectedFileStream,
                It.IsAny<Func<dynamic, Task<bool>>>()))
            .Returns(async (Metadata.Tenant tenant, Metadata.Entity entity, Guid? userId, string identifier, IDictionary<string, object> claims, Stream stream,
                Func<dynamic, Task<bool>> authorize) =>
            {
                await Assert.MultipleAsync(async () =>
                {
                    Assert.That(tenant, Is.EqualTo(expectedTenant));
                    Assert.That(entity, Is.EqualTo(expectedEntity));
                    Assert.That(userId, Is.EqualTo(expectedUserId));
                    Assert.That(identifier, Is.EqualTo(ExpectedFunctionIdentifier));
                    Assert.That(claims, Is.EqualTo(expectedClaims));
                    Assert.That(stream, Is.EqualTo(expectedFileStream));
                    Assert.That(await authorize(expectedEntry), Is.True);
                });
            });

        TenantRightsCheckerMock
            .Setup(c => c.HasRightAsync(expectedTenant, "fakeapplication", "fakeentity", expectedClaims, ExpectedFunctionIdentifier))
            .ReturnsAsync(true);
        
        var jobDataMap = new JobDataMap
        {
            { "tenantId", expectedTenantId },
            { "jobId", expectedJobId },
            { "userId", expectedUserId },
            { "claims", JsonConvert.SerializeObject(expectedClaims) },
            { "file", expectedTemporaryId }
        };

        JobExecutionContextMock
            .Setup(c => c.MergedJobDataMap)
            .Returns(jobDataMap);
        
        var job = new GenericImportJob(
            GenericProviderMock.Object,
            TenantRightsCheckerMock.Object,
            EntityRightsCheckerMock.Object,
            MetadataAdapterMock.Object,
            JobsFileStorageAdapterMock.Object);
        
        // Act
        Assert.ThrowsAsync<JobExecutionException>(async () => await job.Execute(JobExecutionContextMock.Object), $"Tenant {expectedTenantId} unknown");
        
        // Assert
        MetadataAdapterMock.Verify(
            r => r.UpdateJobForTenantBehalfOfUserAsync(expectedTenantId, expectedUserId, It.IsAny<JobUpdatePayload>()),
            Times.Once);
        
        JobsFileStorageAdapterMock.Verify(s => s.TemporaryFileByIdAsync(expectedTenantId, expectedTemporaryId), Times.Never);
        JobsFileStorageAdapterMock.Verify(s => s.RemoveTemporaryFileByIdBehalfOfUserAsync(expectedTenantId, expectedUserId, expectedTemporaryId), Times.Never);
        
        GenericProviderMock.Verify(r => r.ImportAsync(
            expectedTenant,
            expectedEntity,
            ExpectedFunctionIdentifier,
            expectedUserId,
            expectedClaims,
            expectedFileStream,
            It.IsAny<Func<dynamic, Task<bool>>>()), Times.Never);
    }
}
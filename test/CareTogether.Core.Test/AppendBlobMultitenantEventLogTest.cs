﻿using Azure.Storage.Blobs;
using CareTogether.Resources;
using CareTogether.Resources.Models;
using CareTogether.Resources.Storage;
using CareTogether.TestData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CareTogether.Core.Test
{
    [TestClass]
    public class AppendBlobMultitenantEventLogTest
    {
        private static readonly BlobServiceClient testingClient = new BlobServiceClient("UseDevelopmentStorage=true");
        private static Guid Id(char x) => Guid.Parse("xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx".Replace('x', x));

        // 'organizationId' and 'locationId' must be seeded with 1 and 2, respectively, because
        // PopulateTestDataAsync creates data in that organizationId and locationId only.
        static readonly Guid organizationId = Id('1');
        static readonly Guid locationId = Id('2');
        static readonly Guid guid3 = Id('3');
        static readonly Guid guid4 = Id('4');

        static readonly PersonCommandExecuted personCommand = new PersonCommandExecuted(guid4, new DateTime(2021, 7, 1),
            new CreatePerson(guid3, guid4, "Jane", "Smith", Gender.Female, new AgeInYears(42, new DateTime(2021, 1, 1)), "Ethnic",
                ImmutableList<Address>.Empty, null, ImmutableList<PhoneNumber>.Empty, null, ImmutableList<EmailAddress>.Empty, null,
                null, null));

#nullable disable
        AppendBlobMultitenantEventLog<DirectoryEvent> directoryEventLog;
        AppendBlobMultitenantEventLog<ReferralEvent> referralsEventLog;
#nullable restore

        [TestInitialize]
        public void TestInitialize()
        {
            testingClient.GetBlobContainerClient(organizationId.ToString()).DeleteIfExists();
            testingClient.GetBlobContainerClient(guid3.ToString()).DeleteIfExists();

            directoryEventLog = new AppendBlobMultitenantEventLog<DirectoryEvent>(testingClient, "DirectoryEventLog");
            referralsEventLog = new AppendBlobMultitenantEventLog<ReferralEvent>(testingClient, "ReferralsEventLog");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            testingClient.GetBlobContainerClient(organizationId.ToString()).DeleteIfExists();
            testingClient.GetBlobContainerClient(guid3.ToString()).DeleteIfExists();
        }

        [TestMethod]
        public async Task ResultsFromContainerAfterTestDataPopulationMatchesExpected()
        {
            await TestDataProvider.PopulateDirectoryEvents(directoryEventLog);
            await TestDataProvider.PopulateReferralEvents(referralsEventLog);

            var directoryEvents = await directoryEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();

            Assert.AreEqual(43, directoryEvents.Count);
            Assert.AreEqual(typeof(FamilyCommandExecuted), directoryEvents[10].DomainEvent.GetType());
            Assert.AreEqual(typeof(PersonCommandExecuted), directoryEvents[38].DomainEvent.GetType());

            var referralEvents = await referralsEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();

            Assert.AreEqual(24, referralEvents.Count);
            Assert.AreEqual(typeof(ArrangementCommandExecuted), referralEvents[8].DomainEvent.GetType());
        }

        [TestMethod]
        public async Task GettingUninitializedTenantLogReturnsEmptySequence()
        {
            var result = directoryEventLog.GetAllEventsAsync(organizationId, locationId);
            Assert.AreEqual(0, await result.CountAsync());
        }

        //[TestMethod]
        //public async Task GettingPreviouslyInitializedTenantLogReturnsSameSequence()
        //{
        //    var result1 = directoryEventLog.GetAllEventsAsync(organizationId, locationId);
        //    var result2 = directoryEventLog.GetAllEventsAsync(organizationId, locationId);
        //    Assert.AreEqual(0, await result1.CountAsync());
        //    Assert.AreEqual(0, await result2.CountAsync());
        //}

        [TestMethod]
        public async Task AppendingAnEventToAnUninitializedTenantLogStoresItWithTheCorrectSequenceNumber()
        {
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 1);
            var getResult = await directoryEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();
            Assert.AreEqual(1, getResult.Count);
            Assert.AreEqual(1, getResult[0].SequenceNumber);
        }

        //TODO: Reenable this test once the corresponding bug is fixed.
        //[TestMethod]
        //public async Task AppendingAnEventToAnUninitializedTenantLogValidatesTheExpectedSequenceNumber()
        //{
        //    await Assert.ThrowsExceptionAsync<Exception>(() => directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 2));
        //    var getResult = await directoryEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();
        //    Assert.AreEqual(1, getResult.Count);
        //}

        // can't really test already initialized container since we can't guarantee test execution order
        //[TestMethod]
        //public async Task AppendingMultipleEventsToAnUninitializedTenantLogStoresThemCorrectly()
        //{
        //    var appendResult1 = await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 1);
        //    var appendResult2 = await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 2);
        //    var appendResult3 = await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 3);
        //    var getResult = await directoryEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();
        //    Assert.IsTrue(appendResult1.IsT0);
        //    Assert.IsTrue(appendResult2.IsT0);
        //    Assert.IsTrue(appendResult3.IsT0);
        //    Assert.AreEqual(3, getResult.Count);
        //    Assert.AreEqual((personCommand, 1), getResult[0]);
        //    Assert.AreEqual((personCommand, 2), getResult[1]);
        //    Assert.AreEqual((personCommand, 3), getResult[2]);
        //}

        //[TestMethod]
        //public async Task AppendingMultipleEventsToAnInitializedTenantLogStoresThemCorrectly()
        //{
        //    var appendResult1 = await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 4);
        //    var appendResult2 = await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 5);
        //    var appendResult3 = await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 6);
        //    var getResult = await directoryEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();
        //    Assert.IsTrue(appendResult1.IsT0);
        //    Assert.IsTrue(appendResult2.IsT0);
        //    Assert.IsTrue(appendResult3.IsT0);
        //    Assert.AreEqual(6, getResult.Count);
        //    Assert.AreEqual((personCommand, 4), getResult[3]);
        //    Assert.AreEqual((personCommand, 5), getResult[4]);
        //    Assert.AreEqual((personCommand, 6), getResult[5]);
        //}

        [TestMethod]
        public async Task AppendingMultipleEventsToMultipleTenantLogsMaintainsSeparation()
        {
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 1);
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 2);
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 3);
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 4);
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 5);
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 6);
            await directoryEventLog.AppendEventAsync(guid3, guid4, personCommand, 1);
            await directoryEventLog.AppendEventAsync(guid3, guid4, personCommand, 2);
            await directoryEventLog.AppendEventAsync(guid3, guid4, personCommand, 3);
            await directoryEventLog.AppendEventAsync(organizationId, locationId, personCommand, 7);

            var getResult = await directoryEventLog.GetAllEventsAsync(organizationId, locationId).ToListAsync();
            Assert.AreEqual(7, getResult.Count);
            Assert.AreEqual((personCommand, 1), getResult[0]);
            Assert.AreEqual((personCommand, 2), getResult[1]);
            Assert.AreEqual((personCommand, 3), getResult[2]);
        }

        [TestMethod]
        public void BlobNumberCalculatedCorrectly()
        {
            var firstResult = directoryEventLog.getBlobNumber(1);
            var secondResult = directoryEventLog.getBlobNumber(49999);
            var thirdResult = directoryEventLog.getBlobNumber(50000);
            var fourthResult = directoryEventLog.getBlobNumber(50001);
            var fifthResult = directoryEventLog.getBlobNumber(485919);

            Assert.AreEqual(1, firstResult);
            Assert.AreEqual(1, secondResult);
            Assert.AreEqual(1, thirdResult);
            Assert.AreEqual(2, fourthResult);
            Assert.AreEqual(10, fifthResult);
        }

        [TestMethod]
        public void BlockNumberCalculatedCorrectly()
        {
            var firstResult = directoryEventLog.getBlockNumber(1);
            var secondResult = directoryEventLog.getBlockNumber(49999);
            var thirdResult = directoryEventLog.getBlockNumber(50000);
            var fourthResult = directoryEventLog.getBlockNumber(50001);
            var fifthResult = directoryEventLog.getBlockNumber(485919);

            Assert.AreEqual(1, firstResult);
            Assert.AreEqual(49999, secondResult);
            Assert.AreEqual(50000, thirdResult);
            Assert.AreEqual(1, fourthResult);
            Assert.AreEqual(35919, fifthResult);
        }
    }
}

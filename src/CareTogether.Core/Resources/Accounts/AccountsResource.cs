﻿using Azure.Storage.Blobs;
using CareTogether.Resources.Policies;
using CareTogether.Utilities.EventLog;
using CareTogether.Utilities.ObjectStore;
using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;

namespace CareTogether.Resources.Accounts
{
    public sealed class AccountsResource : IAccountsResource
    {
        private const int GLOBAL_SCOPE_ID = 0; // There is only one globally-scoped accounts model.
        private readonly IEventLog<AccountEvent> accountsEventLog;
        private readonly IEventLog<PersonAccessEvent> personAccessEventLog;
        private readonly BlobServiceClient blobServiceClient;
        private readonly IObjectStore<OrganizationConfiguration> organizationConfigurationStore;
        private readonly ConcurrentLockingStore<int, AccountsModel> globalScopeAccountsModel;
        private readonly IObjectStore<UserTenantAccessSummary> configurationStore;
        private readonly ConcurrentLockingStore<(Guid organizationId, Guid locationId), PersonAccessModel> tenantModels;


        public AccountsResource(IObjectStore<UserTenantAccessSummary> configurationStore,
            IEventLog<AccountEvent> accountsEventLog, IEventLog<PersonAccessEvent> personAccessEventLog,
            BlobServiceClient blobServiceClient, IObjectStore<OrganizationConfiguration> organizationConfigurationStore)
        {
            this.configurationStore = configurationStore;
            this.accountsEventLog = accountsEventLog;
            this.personAccessEventLog = personAccessEventLog;
            this.blobServiceClient = blobServiceClient;
            this.organizationConfigurationStore = organizationConfigurationStore;

            globalScopeAccountsModel = new ConcurrentLockingStore<int, AccountsModel>(async key =>
            {
                await MigrateConfigurationStoreToEventLogsAsync();
                return await AccountsModel.InitializeAsync(accountsEventLog.GetAllEventsAsync(Guid.Empty, Guid.Empty));
            });
            tenantModels = new ConcurrentLockingStore<(Guid organizationId, Guid locationId), PersonAccessModel>(key =>
                PersonAccessModel.InitializeAsync(personAccessEventLog.GetAllEventsAsync(key.organizationId, key.locationId)));
        }


        public async Task<Account> GetUserAccountAsync(Guid userId)
        {
            //WARNING: The read/write logic in this service needs to be designed very carefully to avoid deadlocks.

            // First, look up the global account entry to determine which person access records to retrieve.
            AccountEntry accountEntry;
            using (var lockedModel = await globalScopeAccountsModel.ReadLockItemAsync(GLOBAL_SCOPE_ID))
            {
                accountEntry = lockedModel.Value.GetAccount(userId);
            }

            // Then, retrieve and merge all the person access records that are linked to this user account.
            var account = await RenderAccountAsync(accountEntry);
            return account;
        }

        public async Task<Account?> GetPersonUserAccountAsync(Guid organizationId, Guid locationId, Guid personId)
        {
            //WARNING: The read/write logic in this service needs to be designed very carefully to avoid deadlocks.

            AccountEntry? accountEntry;
            using (var lockedModel = await globalScopeAccountsModel.ReadLockItemAsync(GLOBAL_SCOPE_ID))
            {
                var result = lockedModel.Value.FindAccounts(account =>
                    account.PersonLinks.Any(link => link.OrganizationId == organizationId &&
                        link.LocationId == locationId &&
                        link.PersonId == personId));
                
                accountEntry = result.SingleOrDefault();
            }

            var account = accountEntry == null ? null : await RenderAccountAsync(accountEntry);
            return account;
        }

        public async Task<Account> ExecuteAccountCommandAsync(AccountCommand command, Guid userId)
        {
            //WARNING: The read/write logic in this service needs to be designed very carefully to avoid deadlocks.
            
            AccountEntry accountEntry;
            using (var lockedModel = await globalScopeAccountsModel.WriteLockItemAsync(GLOBAL_SCOPE_ID))
            {
                var result = lockedModel.Value.ExecuteAccountCommand(command, userId, DateTime.UtcNow);

                await accountsEventLog.AppendEventAsync(Guid.Empty, Guid.Empty, result.Event, result.SequenceNumber);
                result.OnCommit();
                accountEntry = result.Account;
            }

            var account = await RenderAccountAsync(accountEntry);
            return account;
        }

        public async Task<Account> ExecutePersonAccessCommandAsync(Guid organizationId, Guid locationId,
            PersonAccessCommand command, Guid userId)
        {
            //WARNING: The read/write logic in this service needs to be designed very carefully to avoid deadlocks.

            //TODO: Implement!
            throw new NotImplementedException();
        }

        public async Task<byte[]> CreateUserInviteNonceAsync(Guid organizationId, Guid locationId, Guid personId, Guid userId)
        {
            //WARNING: The read/write logic in this service needs to be designed very carefully to avoid deadlocks.

            //TODO: Implement!
            throw new NotImplementedException();
        }

        public async Task<Account> RedeemUserInviteNonceAsync(Guid organizationId, Guid locationId, Guid userId, byte[] nonce)
        {
            //WARNING: The read/write logic in this service needs to be designed very carefully to avoid deadlocks.

            //TODO: Implement!
            throw new NotImplementedException();
        }


        private async Task<Account> RenderAccountAsync(AccountEntry accountEntry)
        {
            var personAccessResults = (await Task.WhenAll(
                accountEntry.PersonLinks.Select(async link =>
                {
                    using (var lockedModel = await tenantModels.ReadLockItemAsync((link.OrganizationId, link.LocationId)))
                    {
                        return (link.OrganizationId, link.LocationId, lockedModel.Value.GetAccess(link.PersonId));
                    }
                }))).ToDictionary(
                    result => (result.OrganizationId, result.LocationId),
                    result => result.Item3);

            return new Account(accountEntry.UserId, accountEntry.PersonLinks
                .GroupBy(link => link.OrganizationId)
                .Select(orgLinks => new UserOrganizationAccess(orgLinks.Key, orgLinks.Select(link =>
                    new UserLocationAccess(link.LocationId, link.PersonId,
                        personAccessResults[(link.OrganizationId, link.LocationId)].Roles)).ToImmutableList()))
                .ToImmutableList());
        }

        private async Task MigrateConfigurationStoreToEventLogsAsync()
        {
            DateTime migrationTimestamp = DateTime.UtcNow;
            Guid migrationUserId = SystemConstants.SystemUserId;
            var synthesizedAccountEvents = new ConcurrentQueue<AccountEvent>();
            var synthesizedPersonAccessEvents = new ConcurrentQueue<(Guid organizationId, Guid locationId, PersonAccessEvent)>();

            var organizationIds = blobServiceClient.GetBlobContainersAsync()
                .Select(container => Guid.TryParse(container.Name, out var orgId) ? orgId : Guid.Empty)
                .Where(orgId => orgId != Guid.Empty);

            var organizationUserAccess = new ConcurrentDictionary<Guid, (Guid orgId, UserAccessConfiguration access)>();
            await Parallel.ForEachAsync(organizationIds, async (organizationId, _) =>
            {
                var orgConfig = await organizationConfigurationStore.GetAsync(organizationId, Guid.Empty, "config");
                foreach (var userAccess in orgConfig.Users)
                    organizationUserAccess.TryAdd(userAccess.Key, (organizationId, userAccess.Value));
            });

            var migratedPersonAccountLinks = await accountsEventLog.GetAllEventsAsync(Guid.Empty, Guid.Empty)
                .Where(e => e.DomainEvent.Command is LinkPersonToAcccount)
                .ToDictionaryAsync(e => e.DomainEvent.Command.UserId);

            var userIdsWithoutMigratedPersonAccountLinks = configurationStore.ListAsync(Guid.Empty, Guid.Empty)
                .Select(oldAccountId => Guid.Parse(oldAccountId))
                .Where(oldAccountId => !migratedPersonAccountLinks.ContainsKey(oldAccountId));

            await Parallel.ForEachAsync(userIdsWithoutMigratedPersonAccountLinks, async (oldAccountId, _) =>
            {
                var oldAccount = await configurationStore.GetAsync(Guid.Empty, Guid.Empty, oldAccountId.ToString());

                var hasOldAccess = organizationUserAccess.TryGetValue(oldAccountId, out var oldAccess);

                if (!hasOldAccess)
                    return;

                foreach (var oldLocationAccess in oldAccess.access.LocationRoles)
                {
                    //TODO: First, support (and add) per-location person IDs to do this correctly!
                    var linkPersonToAccountEvent = new AccountEvent(migrationUserId, migrationTimestamp,
                        new LinkPersonToAcccount(oldAccountId, oldAccess.orgId, oldLocationAccess.LocationId,
                            oldAccess.access.PersonId));

                    synthesizedAccountEvents.Enqueue(linkPersonToAccountEvent);

                    //HACK: This is simplistic, but since the migration will be a one-time event
                    //      we don't need to address the partial failure mode where account events
                    //      are migrated but person access events are not.
                    //TODO: First, support (and add) per-location person IDs to do this correctly!
                    synthesizedPersonAccessEvents.Enqueue(
                        (oldAccess.access.PersonId, oldAccess.orgId, new PersonAccessCommandExecuted(
                            migrationUserId, migrationTimestamp, new ChangePersonRoles(
                                oldLocationAccess.LocationId, oldLocationAccess.RoleNames))));
                }
            });

            for (var i = 0; synthesizedAccountEvents.TryDequeue(out var domainEvent); i++)
                await accountsEventLog.AppendEventAsync(Guid.Empty, Guid.Empty, domainEvent, i);

            for (var i = 0; synthesizedPersonAccessEvents.TryDequeue(out var domainEvent); i++)
                await personAccessEventLog.AppendEventAsync(
                    domainEvent.organizationId, domainEvent.locationId, domainEvent.Item3, i);
        }
    }
}

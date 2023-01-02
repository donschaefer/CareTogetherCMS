﻿using CareTogether.Engines.Authorization;
using CareTogether.Resources.Approvals;
using CareTogether.Resources.Directory;
using CareTogether.Resources.Notes;
using CareTogether.Resources.Policies;
using CareTogether.Resources.Referrals;
using CareTogether.Utilities.Telephony;
using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CareTogether.Managers.Records
{
    public sealed class RecordsManager : IRecordsManager
    {
        private readonly IAuthorizationEngine authorizationEngine;
        private readonly IDirectoryResource directoryResource;
        private readonly IApprovalsResource approvalsResource;
        private readonly IReferralsResource referralsResource;
        private readonly INotesResource notesResource;
        private readonly IPoliciesResource policiesResource;
        private readonly ITelephony telephony;
        private readonly CombinedFamilyInfoFormatter combinedFamilyInfoFormatter;


        public RecordsManager(IAuthorizationEngine authorizationEngine, IDirectoryResource directoryResource,
            IApprovalsResource approvalsResource, IReferralsResource referralsResource, INotesResource notesResource,
            IPoliciesResource policiesResource, ITelephony telephony, CombinedFamilyInfoFormatter combinedFamilyInfoFormatter)
        {
            this.authorizationEngine = authorizationEngine;
            this.directoryResource = directoryResource;
            this.approvalsResource = approvalsResource;
            this.referralsResource = referralsResource;
            this.notesResource = notesResource;
            this.policiesResource = policiesResource;
            this.telephony = telephony;
            this.combinedFamilyInfoFormatter = combinedFamilyInfoFormatter;
        }


        public async Task<ImmutableList<CombinedFamilyInfo>> ListVisibleFamiliesAsync(ClaimsPrincipal user, Guid organizationId, Guid locationId)
        {
            var families = await directoryResource.ListFamiliesAsync(organizationId, locationId);

            var visibleFamilies = (await families.Select(async family =>
                {
                    var permissions = await authorizationEngine.AuthorizeUserAccessAsync(organizationId, locationId, user,
                        new FamilyAuthorizationContext(family.Id));
                    return (family, hasPermissions: !permissions.IsEmpty);
                })
                .WhenAll())
                .Where(x => x.hasPermissions)
                .Select(x => x.family)
                .Cast<Family>()
                .ToImmutableList();

            var result = await visibleFamilies
                .Select(family => combinedFamilyInfoFormatter.RenderCombinedFamilyInfoAsync(organizationId, locationId, family.Id, user))
                .WhenAll();
            return result.ToImmutableList();
        }

        public async Task<CombinedFamilyInfo> ExecuteCompositeRecordsCommand(Guid organizationId, Guid locationId,
            ClaimsPrincipal user, CompositeRecordsCommand command)
        {
            var atomicCommands = GenerateAtomicCommandsForCompositeCommand(command).ToImmutableList();

            foreach (var atomicCommand in atomicCommands)
                if (!await AuthorizeCommandAsync(organizationId, locationId, user, atomicCommand))
                    throw new Exception("The user is not authorized to perform this command.");

            foreach (var atomicCommand in atomicCommands)
                await ExecuteCommandAsync(organizationId, locationId, user, atomicCommand);

            var familyResult = await combinedFamilyInfoFormatter.RenderCombinedFamilyInfoAsync(
                organizationId, locationId, command.FamilyId, user);

            return familyResult;
        }

        private IEnumerable<AtomicRecordsCommand> GenerateAtomicCommandsForCompositeCommand(CompositeRecordsCommand command)
        {
            switch (command)
            {
                case AddAdultToFamilyCommand c:
                    {
                        var addresses = c.Address == null ? ImmutableList<Address>.Empty : ImmutableList<Address>.Empty.Add(c.Address);
                        var phoneNumbers = c.PhoneNumber == null ? ImmutableList<PhoneNumber>.Empty : ImmutableList<PhoneNumber>.Empty.Add(c.PhoneNumber);
                        var emailAddresses = c.EmailAddress == null ? ImmutableList<EmailAddress>.Empty : ImmutableList<EmailAddress>.Empty.Add(c.EmailAddress);

                        yield return new PersonRecordsCommand(c.FamilyId,
                            new CreatePerson(c.PersonId, null, c.FirstName, c.LastName,
                                c.Gender, c.Age, c.Ethnicity,
                                addresses, c.Address?.Id,
                                phoneNumbers, c.PhoneNumber?.Id,
                                emailAddresses, c.EmailAddress?.Id,
                                c.Concerns, c.Notes));
                        yield return new FamilyRecordsCommand(
                            new AddAdultToFamily(c.FamilyId, c.PersonId, c.FamilyAdultRelationshipInfo));
                        break;
                    }
                case AddChildToFamilyCommand c:
                    {
                        yield return new PersonRecordsCommand(c.FamilyId,
                            new CreatePerson(c.PersonId, null, c.FirstName, c.LastName,
                                c.Gender, c.Age, c.Ethnicity,
                                ImmutableList<Address>.Empty, null,
                                ImmutableList<PhoneNumber>.Empty, null,
                                ImmutableList<EmailAddress>.Empty, null,
                                c.Concerns, c.Notes));
                        yield return new FamilyRecordsCommand(
                            new AddChildToFamily(c.FamilyId, c.PersonId, c.CustodialRelationships.ToImmutableList()));
                        break;
                    }
                case CreateVolunteerFamilyWithNewAdultCommand c:
                    {
                        var addresses = c.Address == null ? ImmutableList<Address>.Empty : ImmutableList<Address>.Empty.Add(c.Address);
                        var phoneNumbers = c.PhoneNumber == null ? ImmutableList<PhoneNumber>.Empty : ImmutableList<PhoneNumber>.Empty.Add(c.PhoneNumber);
                        var emailAddresses = c.EmailAddress == null ? ImmutableList<EmailAddress>.Empty : ImmutableList<EmailAddress>.Empty.Add(c.EmailAddress);

                        yield return new PersonRecordsCommand(c.FamilyId,
                            new CreatePerson(c.PersonId, null, c.FirstName, c.LastName,
                                c.Gender, c.Age, c.Ethnicity,
                                addresses, c.Address?.Id,
                                phoneNumbers, c.PhoneNumber?.Id,
                                emailAddresses, c.EmailAddress?.Id,
                                c.Concerns, c.Notes));
                        yield return new FamilyRecordsCommand(
                            new CreateFamily(c.FamilyId, c.PersonId,
                                ImmutableList<(Guid, FamilyAdultRelationshipInfo)>.Empty.Add((c.PersonId, c.FamilyAdultRelationshipInfo)),
                                ImmutableList<Guid>.Empty,
                                ImmutableList<CustodialRelationship>.Empty));
                        yield return new FamilyApprovalRecordsCommand(
                            new ActivateVolunteerFamily(c.FamilyId));
                        break;
                    }
                case CreatePartneringFamilyWithNewAdultCommand c:
                    {
                        var addresses = c.Address == null ? ImmutableList<Address>.Empty : ImmutableList<Address>.Empty.Add(c.Address);
                        var phoneNumbers = c.PhoneNumber == null ? ImmutableList<PhoneNumber>.Empty : ImmutableList<PhoneNumber>.Empty.Add(c.PhoneNumber);
                        var emailAddresses = c.EmailAddress == null ? ImmutableList<EmailAddress>.Empty : ImmutableList<EmailAddress>.Empty.Add(c.EmailAddress);

                        yield return new PersonRecordsCommand(c.FamilyId,
                            new CreatePerson(c.PersonId, null, c.FirstName, c.LastName,
                                c.Gender, c.Age, c.Ethnicity,
                                addresses, c.Address?.Id,
                                phoneNumbers, c.PhoneNumber?.Id,
                                emailAddresses, c.EmailAddress?.Id,
                                c.Concerns, c.Notes));
                        yield return new FamilyRecordsCommand(
                            new CreateFamily(c.FamilyId, c.PersonId,
                                ImmutableList<(Guid, FamilyAdultRelationshipInfo)>.Empty.Add((c.PersonId, c.FamilyAdultRelationshipInfo)),
                                ImmutableList<Guid>.Empty,
                                ImmutableList<CustodialRelationship>.Empty));
                        yield return new ReferralRecordsCommand(
                            new CreateReferral(c.FamilyId, c.ReferralId, c.ReferralOpenedAtUtc));
                        break;
                    }
                default:
                    throw new NotImplementedException(
                        $"The command type '{command.GetType().FullName}' has not been implemented.");
            }
        }

        public async Task<ImmutableList<(Guid FamilyId, SmsMessageResult? Result)>> SendSmsToFamilyPrimaryContactsAsync(
            Guid organizationId, Guid locationId, ClaimsPrincipal user,
            ImmutableList<Guid> familyIds, string sourceNumber, string message)
        {
            if (!await authorizationEngine.AuthorizeSendSmsAsync(organizationId, locationId, user))
                throw new Exception("The user is not authorized to perform this command.");

            // Validate that the requested source number has been configured for the specified location.
            var configuration = await policiesResource.GetConfigurationAsync(organizationId);
            var sourcePhoneNumber = configuration.Locations
                .Single(location => location.Id == locationId)
                .SmsSourcePhoneNumbers.SingleOrDefault(number => number.SourcePhoneNumber == sourceNumber)
                ?.SourcePhoneNumber;

            if (sourcePhoneNumber == null)
                throw new InvalidOperationException(
                    "The specified location does not have the requested source phone number configured for SMS messages.");

            var families = await directoryResource.ListFamiliesAsync(organizationId, locationId);

            var familyPrimaryContactNumbers = familyIds
                .Select(familyId =>
                {
                    var family = families.Single(family => family.Id == familyId);
                    var primaryContactAdult = family.Adults
                        .Single(adult => adult.Item1.Id == family.PrimaryFamilyContactPersonId);
                    return (familyId,
                        phoneNumber: primaryContactAdult.Item1.PhoneNumbers
                            .SingleOrDefault(number => number.Id == primaryContactAdult.Item1.PreferredPhoneNumberId));
                }).ToImmutableList();

            var destinationNumbers = familyPrimaryContactNumbers
                .Where(x => x.phoneNumber != null)
                .Select(x => x.phoneNumber!.Number)
                .Distinct()
                .ToImmutableList();

            var sendResults = await telephony.SendSmsMessageAsync(sourcePhoneNumber, destinationNumbers, message);

            var allFamilyResults = familyPrimaryContactNumbers.Select(x =>
            {
                var sendResult = sendResults.SingleOrDefault(result => result.PhoneNumber == x.phoneNumber?.Number);
                return (FamilyId: x.familyId, Result: sendResult);
            }).ToImmutableList();

            return allFamilyResults;
        }

        public async Task<CombinedFamilyInfo> ExecuteAtomicRecordsCommandAsync(Guid organizationId, Guid locationId,
            ClaimsPrincipal user, AtomicRecordsCommand command)
        {
            if (!await AuthorizeCommandAsync(organizationId, locationId, user, command))
                throw new Exception("The user is not authorized to perform this command.");

            await ExecuteCommandAsync(organizationId, locationId, user, command);

            var familyId = GetFamilyIdFromCommand(command);

            var familyResult = await combinedFamilyInfoFormatter.RenderCombinedFamilyInfoAsync(
                organizationId, locationId, familyId, user);

            return familyResult;
        }


        private Task<bool> AuthorizeCommandAsync(Guid organizationId, Guid locationId,
            ClaimsPrincipal user, AtomicRecordsCommand command) =>
            command switch
            {
                FamilyRecordsCommand c => authorizationEngine.AuthorizeFamilyCommandAsync(
                    organizationId, locationId, user, c.Command),
                PersonRecordsCommand c => authorizationEngine.AuthorizePersonCommandAsync(
                    organizationId, locationId, user, c.FamilyId, c.Command),
                FamilyApprovalRecordsCommand c => authorizationEngine.AuthorizeVolunteerFamilyCommandAsync(
                    organizationId, locationId, user, c.Command),
                IndividualApprovalRecordsCommand c => authorizationEngine.AuthorizeVolunteerCommandAsync(
                    organizationId, locationId, user, c.Command),
                ReferralRecordsCommand c => authorizationEngine.AuthorizeReferralCommandAsync(
                    organizationId, locationId, user, c.Command),
                ArrangementRecordsCommand c => authorizationEngine.AuthorizeArrangementsCommandAsync(
                    organizationId, locationId, user, c.Command),
                NoteRecordsCommand c => authorizationEngine.AuthorizeNoteCommandAsync(
                    organizationId, locationId, user, c.Command),
                _ => throw new NotImplementedException(
                    $"The command type '{command.GetType().FullName}' has not been implemented.")
            };

        private Task ExecuteCommandAsync(Guid organizationId, Guid locationId,
            ClaimsPrincipal user, AtomicRecordsCommand command) =>
            command switch
            {
                FamilyRecordsCommand c => directoryResource.ExecuteFamilyCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                PersonRecordsCommand c => directoryResource.ExecutePersonCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                FamilyApprovalRecordsCommand c => approvalsResource.ExecuteVolunteerFamilyCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                IndividualApprovalRecordsCommand c => approvalsResource.ExecuteVolunteerCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                ReferralRecordsCommand c => referralsResource.ExecuteReferralCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                ArrangementRecordsCommand c => referralsResource.ExecuteArrangementsCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                NoteRecordsCommand c => notesResource.ExecuteNoteCommandAsync(
                    organizationId, locationId, c.Command, user.UserId()),
                _ => throw new NotImplementedException(
                    $"The command type '{command.GetType().FullName}' has not been implemented.")
            };

        private Guid GetFamilyIdFromCommand(AtomicRecordsCommand command) =>
            command switch
            {
                FamilyRecordsCommand c => c.Command.FamilyId,
                PersonRecordsCommand c => c.FamilyId,
                FamilyApprovalRecordsCommand c => c.Command.FamilyId,
                IndividualApprovalRecordsCommand c => c.Command.FamilyId,
                ReferralRecordsCommand c => c.Command.FamilyId,
                ArrangementRecordsCommand c => c.Command.FamilyId,
                NoteRecordsCommand c => c.Command.FamilyId,
                _ => throw new NotImplementedException(
                    $"The command type '{command.GetType().FullName}' has not been implemented.")
            };
    }
}
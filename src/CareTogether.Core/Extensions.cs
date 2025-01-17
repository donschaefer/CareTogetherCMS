﻿using System;
using System.Collections.Immutable;
using System.Linq;
using System.Security.Claims;

namespace CareTogether
{
    public static class Extensions
    {
        public static ImmutableList<T> With<T>(this ImmutableList<T> list, T valueToUpdate, Predicate<T> predicate)
        {
            return list.Select(x => predicate(x) ? valueToUpdate : x).ToImmutableList();
        }

        public static ImmutableList<T> UpdateSingle<T>(this ImmutableList<T> list, Func<T, bool> predicate,
            Func<T, T> selector)
        {
            var oldValue = list.Single(predicate);
            var newValue = selector(oldValue);
            return list.Replace(oldValue, newValue);
        }

        public static ImmutableList<U> GetValueOrEmptyList<T, U>(this ImmutableDictionary<T, ImmutableList<U>> dictionary, T key)
            where T : notnull
        {
            return dictionary.TryGetValue(key, out var value)
                ? value
                : ImmutableList<U>.Empty;
        }


        public static Guid UserId(this ClaimsPrincipal principal)
        {
            try
            {
                return Guid.Parse(principal.FindFirst(Claims.UserId)!.Value);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The principal does not have a valid user ID claim.", ex);
            }
        }

        public static Guid PersonId(this ClaimsPrincipal principal,
            Guid organizationId, Guid locationId)
        {
            var locationIdentity = principal.LocationIdentity(organizationId, locationId);
            if (locationIdentity != null)
            {
                var personIdClaim = locationIdentity.FindFirst(Claims.PersonId);
                if (personIdClaim != null)
                    return Guid.Parse(personIdClaim.Value);
            }

            throw new InvalidOperationException(
                $"The principal does not have a valid person ID claim for organization '{organizationId}' and location '{locationId}'.");
        }

        public static void AddClaimOnlyOnce(this ClaimsPrincipal principal,
            ClaimsIdentity identity, string type, string value)
        {
            if (!principal.HasClaim(x => x.Type == type))
                identity.AddClaim(new Claim(type, value));
        }

        public static bool CanAccess(this ClaimsPrincipal principal,
            Guid organizationId, Guid locationId)
        {
            var locationIdentity = principal.LocationIdentity(organizationId, locationId);

            return locationIdentity != null &&
                locationIdentity.HasClaim(Claims.OrganizationId, organizationId.ToString()) &&
                locationIdentity.HasClaim(Claims.LocationId, locationId.ToString());
        }

        public static ClaimsIdentity? LocationIdentity(this ClaimsPrincipal principal,
            Guid organizationId, Guid locationId) =>
            principal.Identities
                .SingleOrDefault(identity => identity.AuthenticationType == $"{organizationId}:{locationId}");
    }
}

﻿using System;

namespace CareTogether.Managers
{
    public sealed record VolunteerFamilyProfile(Guid FamilyId);

    /// <summary>
    /// The <see cref="IApprovalManager"/> models the lifecycle of people's approval status with CareTogether organizations,
    /// including various forms, approval, renewals, and policy changes, as well as authorizing related queries.
    /// </summary>
    public interface IApprovalManager
    {
    }
}

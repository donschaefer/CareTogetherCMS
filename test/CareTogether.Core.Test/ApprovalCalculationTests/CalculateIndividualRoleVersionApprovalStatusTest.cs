using System;
using System.Linq;
using CareTogether.Engines.PolicyEvaluation;
using CareTogether.Resources;
using CareTogether.Resources.Policies;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Timelines;
using H = CareTogether.Core.Test.ApprovalCalculationTests.Helpers;

namespace CareTogether.Core.Test.ApprovalCalculationTests
{
    [TestClass]
    public class CalculateIndividualRoleVersionApprovalStatusTest
    {
        [TestMethod]
        public void WhenNoneCompleted()
        {
            var result = IndividualApprovalCalculations.CalculateIndividualRoleVersionApprovalStatus(
                new VolunteerRolePolicyVersion("v1", SupersededAtUtc: H.DT(20),
                    H.IndividualApprovalRequirements(
                        (RequirementStage.Application, "A"),
                        (RequirementStage.Approval, "B"),
                        (RequirementStage.Approval, "C"),
                        (RequirementStage.Approval, "D"),
                        (RequirementStage.Onboarding, "E"),
                        (RequirementStage.Onboarding, "F"))),
                completedRequirements: [
                ],
                exemptedRequirements: [
                ]
            );

            Assert.AreEqual("v1", result.Version);
            Assert.AreEqual(null, result.Status);
            Assert.IsTrue(result.Requirements.SequenceEqual([
                new("A", RequirementStage.Application,
                    WhenMet: null),
                new("B", RequirementStage.Approval,
                    WhenMet: null),
                new("C", RequirementStage.Approval,
                    WhenMet: null),
                new("D", RequirementStage.Approval,
                    WhenMet: null),
                new("E", RequirementStage.Onboarding,
                    WhenMet: null),
                new("F", RequirementStage.Onboarding,
                    WhenMet: null)
            ]));
        }

        [TestMethod]
        public void WhenSomeCompletedAndSomeExempted()
        {
            var result = IndividualApprovalCalculations.CalculateIndividualRoleVersionApprovalStatus(
                new VolunteerRolePolicyVersion("v1", SupersededAtUtc: H.DT(20),
                    H.IndividualApprovalRequirements(
                        (RequirementStage.Application, "A"),
                        (RequirementStage.Approval, "B"),
                        (RequirementStage.Approval, "C"),
                        (RequirementStage.Approval, "D"),
                        (RequirementStage.Onboarding, "E"),
                        (RequirementStage.Onboarding, "F"))),
                completedRequirements: [
                    new CompletedRequirementInfo(H.guid1, DateTime.Now, H.guid1,
                        RequirementName: "A", CompletedAtUtc: H.DT(5), ExpiresAtUtc: H.DT(12),
                        null, null),
                    new CompletedRequirementInfo(H.guid1, DateTime.Now, H.guid2,
                        RequirementName: "B", CompletedAtUtc: H.DT(7), ExpiresAtUtc: null,
                        null, null),
                    new CompletedRequirementInfo(H.guid1, DateTime.Now, H.guid3,
                        RequirementName: "A", CompletedAtUtc: H.DT(14), ExpiresAtUtc: null,
                        null, null),
                    new CompletedRequirementInfo(H.guid1, DateTime.Now, H.guid4,
                        RequirementName: "C", CompletedAtUtc: H.DT(10), ExpiresAtUtc: null,
                        null, null),
                    new CompletedRequirementInfo(H.guid1, DateTime.Now, H.guid5,
                        RequirementName: "E", CompletedAtUtc: H.DT(25), ExpiresAtUtc: null,
                        null, null)
                ],
                exemptedRequirements: [
                    new ExemptedRequirementInfo(H.guid1,
                        TimestampUtc: H.DT(11), RequirementName: "D", DueDate: null,
                        AdditionalComments: "", ExemptionExpiresAtUtc: H.DT(20)),
                ]
            );

            Assert.AreEqual("v1", result.Version);
            Assert.AreEqual(new DateOnlyTimeline<RoleApprovalStatus>([
                    H.DR(5, 10, RoleApprovalStatus.Prospective),
                    H.DR(11, 12, RoleApprovalStatus.Approved),
                    H.DR(13, 13, RoleApprovalStatus.Expired),
                    H.DR(14, 20, RoleApprovalStatus.Approved),
                    H.DR(21, null, RoleApprovalStatus.Expired)
                ]), result.Status);
            Assert.IsTrue(result.Requirements.SequenceEqual([
                new("A", RequirementStage.Application,
                    WhenMet: new DateOnlyTimeline([
                        H.DR(5, 12),
                        H.DR(14, null)
                    ])),
                new("B", RequirementStage.Approval,
                    WhenMet: new DateOnlyTimeline([
                        H.DR(7, null)
                    ])),
                new("C", RequirementStage.Approval,
                    WhenMet: new DateOnlyTimeline([
                        H.DR(10, null)
                    ])),
                new("D", RequirementStage.Approval,
                    WhenMet: new DateOnlyTimeline([
                        H.DR(11, 20)
                    ])),
                new("E", RequirementStage.Onboarding,
                    WhenMet: null),
                new("F", RequirementStage.Onboarding,
                    WhenMet: null)
                ]));
        }
    }
}
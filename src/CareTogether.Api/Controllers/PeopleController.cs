﻿using CareTogether.Managers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace CareTogether.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("/api/{organizationId:guid}/{locationId:guid}/[controller]")]
    public class PeopleController : ControllerBase
    {
        private readonly AuthorizationProvider authorizationProvider;
        private readonly IMembershipManager membershipManager;
        private readonly ILogger<ClaimsController> logger;


        public PeopleController(AuthorizationProvider authorizationProvider,
            IMembershipManager membershipManager, ILogger<ClaimsController> logger)
        {
            this.authorizationProvider = authorizationProvider;
            this.membershipManager = membershipManager;
            this.logger = logger;
        }


        [HttpGet]
        public async Task<IActionResult> Get(Guid organizationId, Guid locationId)
        {
            logger.LogInformation("User '{UserName}' was authenticated via '{AuthenticationType}'",
                User.Identity.Name, User.Identity.AuthenticationType);

            var authorizedUser = await authorizationProvider.AuthorizeAsync(organizationId, locationId, User);

            var result = await membershipManager.QueryPeopleAsync(authorizedUser, organizationId, locationId, "");
            if (result.TryPickT0(out var people, out var error))
                return Ok(people);
            else
                return BadRequest(error);
        }

        [HttpGet("{personId:guid}")]
        public async Task<IActionResult> GetContactInfo(Guid organizationId, Guid locationId, Guid personId)
        {
            logger.LogInformation("User '{UserName}' was authenticated via '{AuthenticationType}'",
                User.Identity.Name, User.Identity.AuthenticationType);

            var authorizedUser = await authorizationProvider.AuthorizeAsync(organizationId, locationId, User);

            var result = await membershipManager.QueryPeopleAsync(authorizedUser, organizationId, locationId, "");
            if (result.TryPickT0(out var people, out var error))
            {
                var person = people.SingleOrDefault(person => person.Id == personId);
                if (person != null)
                {
                    var contactInfoResult = await membershipManager.GetContactInfoAsync(authorizedUser, organizationId, locationId, personId);
                    if (contactInfoResult.TryPickT0(out var contactInfo, out var contactInfoError))
                        return Ok(new
                        {
                            Person = person,
                            ContactInfo = contactInfo
                        });
                    else
                        return BadRequest(contactInfoError);
                }
                else
                    return NotFound();
            }
            else
                return BadRequest(error);
        }
    }
}

﻿using CareTogether.Resources;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace CareTogether.Api.Controllers
{
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly IAccountsResource accountsResource;


        public UsersController(IAccountsResource accountsResource)
        {
            this.accountsResource = accountsResource;
        }


        [HttpGet("/api/[controller]/me/tenantAccess")]
        public async Task<ActionResult<UserTenantAccessSummary>> GetUserTenantAccess()
        {
            var tenantAccessSummary = await accountsResource.GetTenantAccessSummaryAsync(User);

            return Ok(tenantAccessSummary);
        }
    }
}
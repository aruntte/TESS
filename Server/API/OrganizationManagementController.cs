using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Remotely.Shared.Models;
using Remotely.Server.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Remotely.Shared.ViewModels.Organization;
using System.Text;
using Microsoft.AspNetCore.WebUtilities;
using Remotely.Server.Attributes;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Remotely.Server.API
{
    [Route("api/[controller]")]
    [ApiController]
    public class OrganizationManagementController : ControllerBase
    {
        public OrganizationManagementController(DataService dataService, UserManager<RemotelyUser> userManager, IEmailSenderEx emailSender)
        {
            this.DataService = dataService;
            this.UserManager = userManager;
            this.EmailSender = emailSender;
        }

        private DataService DataService { get; }
        private IEmailSenderEx EmailSender { get; }
        private UserManager<RemotelyUser> UserManager { get; }


        [HttpPost("ChangeIsAdmin/{userID}")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public IActionResult ChangeIsAdmin(string userID, [FromBody]bool isAdmin)
        {
            if (User.Identity.IsAuthenticated && 
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            if (User.Identity.IsAuthenticated && 
                DataService.GetUserByName(User.Identity.Name).Id == userID)
            {
                return BadRequest("You can't remove administrator rights from yourself.");
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);

            DataService.ChangeUserIsAdmin(orgID, userID, isAdmin);
            return Ok("ok");
        }

        [HttpDelete("DeleteInvite/{inviteID}")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public IActionResult DeleteInvite(string inviteID)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            DataService.DeleteInvite(orgID, inviteID);
            return Ok("ok");
        }

        [HttpDelete("DeleteUser/{userID}")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public async Task<IActionResult> DeleteUser(string userID)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            if (User.Identity.IsAuthenticated &&
              DataService.GetUserByName(User.Identity.Name).Id == userID)
            {
                return BadRequest("You can't delete yourself here.  You must go to the Personal Data page to delete your own account.");
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            await DataService.RemoveUserFromOrganization(orgID, userID);
            return Ok("ok");
        }

        [HttpDelete("DeviceGroup")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public IActionResult DeviceGroup([FromBody]string deviceGroupID)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            DataService.DeleteDeviceGroup(orgID, deviceGroupID.Trim());
            return Ok("ok");
        }

        [HttpPost("DeviceGroup")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public IActionResult DeviceGroup([FromBody]DeviceGroup deviceGroup)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            var result = DataService.AddDeviceGroup(orgID, deviceGroup, out var deviceGroupID, out var errorMessage);
            if (!result)
            {
                return BadRequest(errorMessage);
            }
            return Ok(deviceGroupID);
        }

        [HttpDelete("DeviceGroup/{groupID}/Users/")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public async Task<IActionResult> DeviceGroupRemoveUser([FromBody]string userID, string groupID)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            if (!await DataService.RemoveUserFromDeviceGroup(orgID, groupID, userID))
            {
                return BadRequest("Failed to remove user from group.");
            }
            return Ok();
        }

        [HttpPost("DeviceGroup/{groupID}/Users/")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public IActionResult DeviceGroupAddUser([FromBody]string userID, string groupID)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            var result = DataService.AddUserToDeviceGroup(orgID, groupID, userID, out var resultMessage);
            if (!result)
            {
                return BadRequest(resultMessage);
            }
            
            return Ok(resultMessage);
        }

        [HttpGet("GenerateResetUrl/{userID}")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public async Task<IActionResult> GenerateResetUrl(string userID)
        {
            if (User.Identity.IsAuthenticated &&
              !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);

            var user = await UserManager.FindByIdAsync(userID);

            if (user.OrganizationID != orgID)
            {
                return Unauthorized();
            }

            var code = await UserManager.GeneratePasswordResetTokenAsync(user);
            code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
            var callbackUrl = Url.Page(
                "/Account/ResetPassword",
                pageHandler: null,
                values: new { area = "Identity", code },
                protocol: Request.Scheme);

            return Ok(callbackUrl);

        }

        [HttpPut("Name")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public IActionResult Name([FromBody]string organizationName)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }
            if (organizationName.Length > 25)
            {
                return BadRequest();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);
            DataService.UpdateOrganizationName(orgID, organizationName.Trim());
            return Ok("ok");
        }
        [HttpPost("SendInvite")]
        [ServiceFilter(typeof(ApiAuthorizationFilter))]
        public async Task<IActionResult> SendInvite([FromBody]Invite invite)
        {
            if (User.Identity.IsAuthenticated &&
                !DataService.GetUserByName(User.Identity.Name).IsAdministrator)
            {
                return Unauthorized();
            }
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            Request.Headers.TryGetValue("OrganizationID", out var orgID);


            if (!DataService.DoesUserExist(invite.InvitedUser))
            {
                var result = await DataService.CreateUser(invite.InvitedUser, invite.IsAdmin, orgID);
                if (result)
                {
                    var user = await UserManager.FindByEmailAsync(invite.InvitedUser);

                    await UserManager.ConfirmEmailAsync(user, await UserManager.GenerateEmailConfirmationTokenAsync(user));

                    return Ok();
                }
                else
                {
                    return BadRequest("There was an issue creating the new account.");
                }
            }
            else
            {
                var newInvite = DataService.AddInvite(orgID, invite);

                var inviteURL = $"{Request.Scheme}://{Request.Host}/Invite?id={newInvite.ID}";
                var emailResult = await EmailSender.SendEmailAsync(invite.InvitedUser, "Invitation to Organization in Remotely",
                            $@"<img src='https://remotely.one/media/Remotely_Logo.png'/>
                            <br><br>
                            Hello!
                            <br><br>
                            You've been invited to join an organization in Remotely.
                            <br><br>
                            You can join the organization by <a href='{HtmlEncoder.Default.Encode(inviteURL)}'>clicking here</a>.",
                            orgID);

                if (!emailResult)
                {
                    return Problem("There was an error sending the invitation email.");
                }

                return Ok();
            }
 
        }
    }
}

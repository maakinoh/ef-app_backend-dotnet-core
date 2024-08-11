using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eurofurence.App.Domain.Model.Communication;
using Eurofurence.App.Server.Services.Abstractions.Communication;
using Eurofurence.App.Server.Services.Abstractions.Security;
using Eurofurence.App.Server.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eurofurence.App.Server.Web.Controllers
{
    [Route("Api/[controller]")]
    public class CommunicationController : BaseController
    {
        private readonly IPrivateMessageService _privateMessageService;

        public CommunicationController(IPrivateMessageService privateMessageService)
        {
            _privateMessageService = privateMessageService;
        }

        /// <summary>
        ///     Retrieves all private messages of an authenticated attendee.
        /// </summary>
        /// <remarks>
        ///     This will set the `ReceivedDateTimeUtc` to the current server time on all messages retrieved
        ///     that have not been retrieved in a previous call.
        /// </remarks>
        /// <returns>A list of all private messages for the authorized attendee</returns>
        [Authorize(Roles = "Attendee")]
        [HttpGet("PrivateMessages")]
        [ProducesResponseType(typeof(IEnumerable<PrivateMessageRecord>), 200)]
        public Task<List<PrivateMessageRecord>> GetMyPrivateMessagesAsync(CancellationToken cancellationToken = default)
        {
            return _privateMessageService.GetPrivateMessagesForRecipientAsync(
                User.GetRegSysIds(),
                User.GetSubject(),
                cancellationToken
            );
        }

        /// <summary>
        ///     Marks a given private message as read (reading receipt).
        /// </summary>
        /// <remarks>
        ///     Calling this on a message that has already been marked as read
        ///     will not update the `ReadDateTimeUtc` property, but return the
        ///     `ReadDateTimeUtc` value of the first call.
        /// </remarks>
        /// <param name="messageId">`Id` of the message to mark as read</param>
        /// <param name="isRead">boolean, expected to be 'true' always</param>
        /// <param name="cancellationToken">Token for request cancellation</param>
        /// <returns>The current timestamp on the server that will be persisted in the messages `ReadDateTimeUtc` property.</returns>
        /// <response code="400">`MessageId` is invalid or not accessible by the user.</response>
        [Authorize(Roles = "Attendee")]
        [HttpPost("PrivateMessages/{messageId}/Read")]
        [ProducesResponseType(typeof(DateTime), 200)]
        public async Task<ActionResult> MarkMyPrivateMessageAsReadAsync(
            [EnsureNotNull] [FromRoute] Guid messageId,
            [EnsureNotNull] [FromBody] bool isRead,
            CancellationToken cancellationToken = default
        )
        {
            if (!isRead) return BadRequest("Message can only be marked as read; not as unread.");

            var result = await _privateMessageService.MarkPrivateMessageAsReadAsync(
                messageId,
                User.GetRegSysIds(),
                User.GetSubject(),
                cancellationToken
            );
            return result.HasValue ? Json(result) : BadRequest();
        }

        /// <summary>
        ///     Sends a private message to a specific recipient/attendee.
        /// </summary>
        /// <remarks>
        ///     If the backend has a push-channel available to any given device(s) that are currently signed into the app
        ///     with the same recipient uid, it will push a toast message to those devices.
        ///     The toast message content is defined by the `ToastTitle` and `ToastMessage` properties.
        /// </remarks>
        /// <param name="Request"></param>
        /// <returns>The `Id` of the message that has been delivered.</returns>
        /// <response code="400">Unable to parse `Request`</response>
        [Authorize(Roles = "Admin,PrivateMessageSender")]
        [HttpPost("PrivateMessages")]
        [ProducesResponseType(typeof(Guid), 200)]
        public async Task<ActionResult> SendPrivateMessageAsync(
            [FromBody] SendPrivateMessageByRegSysRequest Request,
            CancellationToken cancellationToken = default)
        {
            if (Request == null) return BadRequest();

            if (User.IsInRole("Attendee"))
            {
                Request.AuthorName = User.GetName();
            }

            return Json(await _privateMessageService.SendPrivateMessageAsync(
                Request,
                User.GetSubject(),
                cancellationToken
            ));
        }
        
        /// <summary>
        ///     Sends a private message to a specific recipient/attendee.
        /// </summary>
        /// <remarks>
        ///     If the backend has a push-channel available to any given device(s) that are currently signed into the app
        ///     with the same recipient uid, it will push a toast message to those devices.
        ///     The toast message content is defined by the `ToastTitle` and `ToastMessage` properties.
        /// </remarks>
        /// <param name="Request"></param>
        /// <returns>The `Id` of the message that has been delivered.</returns>
        /// <response code="400">Unable to parse `Request`</response>
        [Authorize(Roles = "Developer,System,Action-PrivateMessages-Send")]
        [HttpPost("PrivateMessages/Identity")]
        [ProducesResponseType(typeof(Guid), 200)]
        public async Task<ActionResult> SendPrivateMessageIdentityAsync(
            [FromBody] SendPrivateMessageByIdentityRequest Request,
            CancellationToken cancellationToken = default)
        {
            if (Request == null) return BadRequest();

            if (User.IsInRole("Attendee"))
            {
                Request.AuthorName = User.GetName();
            }

            return Json(await _privateMessageService.SendPrivateMessageAsync(
                Request,
                User.GetSubject(),
                cancellationToken
            ));
        }


        [HttpGet("PrivateMessages/{messageId}/Status")]
        [Authorize(Roles = "Admin,PrivateMessageSender")]
        [ProducesResponseType(typeof(PrivateMessageStatus), 200)]
        [ProducesResponseType(typeof(string), 404)]
        public async Task<PrivateMessageStatus> GetPrivateMessageStatusAsync(
            [FromRoute] [EnsureNotNull] Guid messageId,
            CancellationToken cancellationToken = default)
        {
            var result = await _privateMessageService.GetPrivateMessageStatusAsync(messageId, cancellationToken);
            return result.Transient404(HttpContext);
        }

        [HttpGet("PrivateMessages/:sent-by-me")]
        [Authorize]
        [ProducesResponseType(typeof(IEnumerable<PrivateMessageRecord>), 200)]
        public async Task<List<PrivateMessageRecord>> GetMySentPrivateMessagesAsync(
            CancellationToken cancellationToken = default)
        {
            return await _privateMessageService.GetPrivateMessagesForSenderAsync(User.GetSubject(), cancellationToken);
        }

        [HttpGet("NotificationQueue/Count")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(int), 200)]
        public int GetNotificationQueueSize()
        {
            return _privateMessageService.GetNotificationQueueSize();
        }
    }
}
﻿namespace Eurofurence.App.Server.Services.Abstractions.Communication
{
    public class SendPrivateMessageRequest
    {
        public string RecipientUid { get; set; }
        public string AuthorName { get; set; }
        public string ToastTitle { get; set; }
        public string ToastMessage { get; set; }
        public string Subject { get; set; }
        public string Message { get; set; }
    }
}
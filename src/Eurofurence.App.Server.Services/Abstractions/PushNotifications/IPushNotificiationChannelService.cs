﻿using Eurofurence.App.Domain.Model.PushNotifications;

namespace Eurofurence.App.Server.Services.Abstractions.PushNotifications
{
    public interface IPushNotificiationChannelService :
        IEntityServiceOperations<PushNotificationChannelRecord>,
        IPatchOperationProcessor<PushNotificationChannelRecord>
    {
    }
}
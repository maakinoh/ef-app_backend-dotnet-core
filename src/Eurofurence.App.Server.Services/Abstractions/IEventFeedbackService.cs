﻿using Eurofurence.App.Domain.Model.Events;

namespace Eurofurence.App.Server.Services.Abstractions
{
    public interface IEventFeedbackService :
     IEntityServiceOperations<EventFeedbackRecord>,
     IPatchOperationProcessor<EventFeedbackRecord>
    {

    }
}
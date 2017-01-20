using Eurofurence.App.Domain.Model;
using Eurofurence.App.Domain.Model.Abstractions;
using Eurofurence.App.Server.Services.Abstractions;

namespace Eurofurence.App.Server.Services.Events
{
    public class EventConferenceRoomService : EntityServiceBase<EventConferenceRoomRecord>,
        IEventConferenceRoomService
    {
        public EventConferenceRoomService(IEntityRepository<EventConferenceRoomRecord> entityRepository)
            : base(entityRepository)
        {
        }
    }
}
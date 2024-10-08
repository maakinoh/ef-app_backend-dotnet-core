﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using Eurofurence.App.Domain.Model.Images;

namespace Eurofurence.App.Domain.Model.ArtistsAlley
{
    public class TableRegistrationRecord : EntityBase
    {
        public class StateChangeRecord : EntityBase
        {
            public DateTime ChangedDateTimeUtc { get; set; }
            public string ChangedByUid { get; set; }
            public RegistrationStateEnum OldState { get; set; }
            public RegistrationStateEnum NewState { get; set; }
        }

        public enum RegistrationStateEnum
        {
            Pending = 0,
            Accepted = 1,
            Published = 2,
            Rejected = 3
        }

        [DataMember]
        public DateTime CreatedDateTimeUtc { get; set; }

        [DataMember]
        public string OwnerUid { get; set; }

        [DataMember]
        public string OwnerUsername { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        [DataMember]
        public string WebsiteUrl { get; set; }

        [DataMember]
        public string ShortDescription { get; set; }

        [DataMember]
        public string TelegramHandle { get; set; }

        [DataMember]
        public string Location { get; set; }

        [DataMember]
        public Guid? ImageId { get; set; }

        [DataMember]
        public ImageRecord Image { get; set; }

        [DataMember]
        public RegistrationStateEnum State { get; set; } = RegistrationStateEnum.Pending;

        [JsonIgnore]
        public IList<StateChangeRecord> StateChangeLog { get; set; }

        public TableRegistrationRecord()
        {
            this.StateChangeLog = new List<StateChangeRecord>();
        }

        public StateChangeRecord ChangeState(RegistrationStateEnum newState, string uid)
        {
            var stateChange = new StateChangeRecord()
            {
                ChangedByUid = uid,
                ChangedDateTimeUtc = DateTime.UtcNow,
                NewState = newState,
                OldState = State
            };

            StateChangeLog.Add(stateChange);

            State = newState;

            return stateChange;
        }
    }
}

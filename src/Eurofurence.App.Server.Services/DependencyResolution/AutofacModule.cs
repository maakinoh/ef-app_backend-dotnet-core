﻿using Autofac;
using Eurofurence.App.Server.Services.Abstractions;
using Eurofurence.App.Server.Services.Abstractions.Announcements;
using Eurofurence.App.Server.Services.Abstractions.ArtistsAlley;
using Eurofurence.App.Server.Services.Abstractions.ArtShow;
using Eurofurence.App.Server.Services.Abstractions.Communication;
using Eurofurence.App.Server.Services.Abstractions.Dealers;
using Eurofurence.App.Server.Services.Abstractions.Events;
using Eurofurence.App.Server.Services.Abstractions.Fursuits;
using Eurofurence.App.Server.Services.Abstractions.Images;
using Eurofurence.App.Server.Services.Abstractions.Knowledge;
using Eurofurence.App.Server.Services.Abstractions.Maps;
using Eurofurence.App.Server.Services.Abstractions.PushNotifications;
using Eurofurence.App.Server.Services.Abstractions.Security;
using Eurofurence.App.Server.Services.Abstractions.Telegram;
using Eurofurence.App.Server.Services.Abstractions.Validation;
using Eurofurence.App.Server.Services.Announcements;
using Eurofurence.App.Server.Services.ArtistsAlley;
using Eurofurence.App.Server.Services.ArtShow;
using Eurofurence.App.Server.Services.Communication;
using Eurofurence.App.Server.Services.Dealers;
using Eurofurence.App.Server.Services.Events;
using Eurofurence.App.Server.Services.Fursuits;
using Eurofurence.App.Server.Services.Images;
using Eurofurence.App.Server.Services.Knowledge;
using Eurofurence.App.Server.Services.Maps;
using Eurofurence.App.Server.Services.PushNotifications;
using Eurofurence.App.Server.Services.Security;
using Eurofurence.App.Server.Services.Storage;
using Eurofurence.App.Server.Services.Telegram;
using Eurofurence.App.Server.Services.Validation;
using Microsoft.Extensions.Configuration;

namespace Eurofurence.App.Server.Services.DependencyResolution
{
    public class AutofacModule : Module
    {
        private readonly IConfiguration _configuration;

        public AutofacModule(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        protected override void Load(ContainerBuilder builder)
        {
            RegisterConfigurations(builder);
            RegisterServices(builder);
        }

        private void RegisterConfigurations(ContainerBuilder builder)
        {
            if (_configuration == null) return;

            builder.RegisterInstance(ConventionSettings.FromConfiguration(_configuration));
            builder.RegisterInstance(TokenFactorySettings.FromConfiguration(_configuration));
            builder.RegisterInstance(AuthenticationSettings.FromConfiguration(_configuration));
            builder.RegisterInstance(WnsConfiguration.FromConfiguration(_configuration));
            builder.RegisterInstance(FirebaseConfiguration.FromConfiguration(_configuration));
            builder.RegisterInstance(TelegramConfiguration.FromConfiguration(_configuration));
            builder.RegisterInstance(CollectionGameConfiguration.FromConfiguration(_configuration));
            builder.RegisterInstance(ArtistAlleyConfiguration.FromConfiguration(_configuration));
        }

        private void RegisterServices(ContainerBuilder builder)
        {
            builder.RegisterType<AgentClosingResultService>().As<IAgentClosingResultService>();
            builder.RegisterType<AnnouncementService>().As<IAnnouncementService>();
            builder.RegisterType<AuthenticationHandler>().As<IAuthenticationHandler>();
            builder.RegisterType<BotManager>().As<BotManager>();
            builder.RegisterType<CollectingGameService>().As<ICollectingGameService>();
            builder.RegisterType<DealerService>().As<IDealerService>();
            builder.RegisterType<EventConferenceDayService>().As<IEventConferenceDayService>();
            builder.RegisterType<EventConferenceRoomService>().As<IEventConferenceRoomService>();
            builder.RegisterType<EventConferenceTrackService>().As<IEventConferenceTrackService>();
            builder.RegisterType<EventFeedbackService>().As<IEventFeedbackService>();
            builder.RegisterType<EventService>().As<IEventService>();
            builder.RegisterType<FirebaseChannelManager>().As<IFirebaseChannelManager>();
            builder.RegisterType<FursuitBadgeService>().As<IFursuitBadgeService>();
            builder.RegisterType<ImageService>().As<IImageService>();
            builder.RegisterType<ItemActivityService>().As<IItemActivityService>();
            builder.RegisterType<KnowledgeEntryService>().As<IKnowledgeEntryService>();
            builder.RegisterType<KnowledgeGroupService>().As<IKnowledgeGroupService>();
            builder.RegisterType<LinkFragmentValidator>().As<ILinkFragmentValidator>();
            builder.RegisterType<MapService>().As<IMapService>();
            builder.RegisterType<PrivateMessageService>()
                .As<IPrivateMessageService>()
                .SingleInstance();
            builder.RegisterType<PushEventMediator>().As<IPushEventMediator>();
            builder.RegisterType<PushNotificationChannelStatisticsService>().As<IPushNotificationChannelStatisticsService>();
            builder.RegisterType<PushNotificiationChannelService>().As<IPushNotificiationChannelService>();
            builder.RegisterType<RegSysAlternativePinAuthenticationProvider>()
                .As<IRegSysAlternativePinAuthenticationProvider>();
            builder.RegisterType<StorageServiceFactory>().As<IStorageServiceFactory>();
            builder.RegisterType<TableRegistrationService>().As<ITableRegistrationService>();
            builder.RegisterType<TelegramMessageBroker>()
                .As<ITelegramMessageBroker>()
                .As<ITelegramMessageSender>()
                .SingleInstance();
            builder.RegisterType<TokenFactory>().As<ITokenFactory>();
            builder.RegisterType<UserManager>().As<IUserManager>();
            builder.RegisterType<WnsChannelManager>().As<IWnsChannelManager>();
        }
    }
}
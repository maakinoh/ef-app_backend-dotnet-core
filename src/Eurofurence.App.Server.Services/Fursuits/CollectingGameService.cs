﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Eurofurence.App.Common.Results;
using Eurofurence.App.Common.Utility;
using Eurofurence.App.Domain.Model;
using Eurofurence.App.Domain.Model.CollectionGame;
using Eurofurence.App.Domain.Model.Fursuits.CollectingGame;
using Eurofurence.App.Infrastructure.EntityFramework;
using Eurofurence.App.Server.Services.Abstractions;
using Eurofurence.App.Server.Services.Abstractions.Fursuits;
using Eurofurence.App.Server.Services.Abstractions.Telegram;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eurofurence.App.Server.Services.Fursuits
{
    public class CollectingGameService : ICollectingGameService
    {
        private readonly AppDbContext _appDbContext;
        private static SemaphoreSlim _semaphore = new(1, 1);
        private readonly CollectionGameConfiguration _collectionGameConfiguration;
        private readonly ITelegramMessageSender _telegramMessageSender;
        private ILogger _logger;

        public CollectingGameService(
            AppDbContext appDbContext,
            ILoggerFactory loggerFactory,
            CollectionGameConfiguration collectionGameConfiguration,
            ITelegramMessageSender telegramMessageSender
        )
        {
            _appDbContext = appDbContext;
            _logger = loggerFactory.CreateLogger(GetType());
            _collectionGameConfiguration = collectionGameConfiguration;
            _telegramMessageSender = telegramMessageSender;
        }


        public async Task<IEnumerable<FursuitParticipationInfo>> GetFursuitParticipationInfoForOwnerAsync(
            string ownerUid)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: GetFursuitParticipationInfoForOwnerAsync({ownerUid}): {time} ms",
                       ownerUid, time.TotalMilliseconds)))
            {
                var fursuitBadges = await _appDbContext.FursuitBadges.Where(badge => badge.OwnerUid == ownerUid)
                    .ToListAsync();

                var results = await Task.WhenAll(fursuitBadges.Select(fursuitBadge => Task.Run(async () =>
                {
                    var result = new FursuitParticipationInfo() { Badge = fursuitBadge };
                    var participationRecord =
                        await _appDbContext.FursuitParticipations.FirstOrDefaultAsync(
                            fursuit => fursuit.FursuitBadgeId == fursuitBadge.Id);

                    if (participationRecord != null)
                    {
                        result.IsParticipating = true;
                        result.Participation = participationRecord;
                    }

                    return result;
                })));

                return results;
            }
        }

        public async Task<PlayerParticipationInfo> GetPlayerParticipationInfoForPlayerAsync(string playerUid,
            string playerName)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: GetPlayerParticipationInfoForPlayerAsync({playerUid}): {time} ms",
                       playerUid, time.TotalMilliseconds)))
            {
                var response = new PlayerParticipationInfo() { Name = playerName };
                var playerParticipation =
                    await _appDbContext.PlayerParticipations
                        .Include(playerParticipation => playerParticipation.CollectionEntries)
                        .FirstOrDefaultAsync(playerParticipation => playerParticipation.PlayerUid == playerUid);

                if (playerParticipation == null) return response;

                response.CollectionCount = playerParticipation.CollectionCount;
                response.IsBanned = playerParticipation.IsBanned;

                var recentlyCollected = playerParticipation.CollectionEntries
                    .OrderByDescending(a => a.EventDateTimeUtc)
                    .Take(5)
                    .ToList();

                var recentlyCollectedIds = recentlyCollected.Select(a => a.FursuitParticipationId);

                var recentlyCollectedFursuitParticipations =
                    _appDbContext.FursuitParticipations.Where(entity => recentlyCollectedIds.Contains(entity.Id));

                var recentlyCollectedFursuitParticipationIds =
                    recentlyCollectedFursuitParticipations.Select(a => a.FursuitBadgeId);

                var recentlyCollectedFursuitBadges =
                    _appDbContext.FursuitBadges.Where(entity =>
                        recentlyCollectedFursuitParticipationIds.Contains(entity.Id));

                response.RecentlyCollected = recentlyCollected.Select(a =>
                    {
                        var fursuitParticipation =
                            recentlyCollectedFursuitParticipations.Single(b => b.Id == a.FursuitParticipationId);
                        var fursuitBadge =
                            recentlyCollectedFursuitBadges.Single(b => b.Id == fursuitParticipation.FursuitBadgeId);

                        return new PlayerParticipationInfo.BadgeInfo()
                        {
                            Id = fursuitBadge.Id,
                            Name = fursuitBadge.Name
                        };
                    })
                    .ToList();

                var playersAhead = _appDbContext.PlayerParticipations.Where(
                    a => !a.IsBanned && a.PlayerUid != playerParticipation.PlayerUid
                                     && a.CollectionCount >= playerParticipation.CollectionCount);

                response.ScoreboardRank =
                    playersAhead.Count(a => a.CollectionCount > playerParticipation.CollectionCount
                                            || (
                                                a.CollectionCount > 0 &&
                                                a.CollectionCount == playerParticipation.CollectionCount &&
                                                a.CollectionEntries.Max(b => b.EventDateTimeUtc) <
                                                playerParticipation.CollectionEntries.Max(b => b.EventDateTimeUtc)
                                            )) + 1;

                return response;
            }
        }

        public async Task<PlayerCollectionEntry[]> GetPlayerCollectionEntriesForPlayerAsync(string playerUid)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: GetPlayerCollectionEntriesForPlayerAsync({playerUid}): {time} ms",
                       playerUid, time.TotalMilliseconds)))
            {
                var playerParticipation =
                    await _appDbContext.PlayerParticipations
                        .Include(playerParticipationRecord => playerParticipationRecord.CollectionEntries)
                        .FirstOrDefaultAsync(a => a.PlayerUid == playerUid);
                if (playerParticipation == null) return new PlayerCollectionEntry[0];

                var collectionEntries = playerParticipation.CollectionEntries
                    .OrderByDescending(a => a.EventDateTimeUtc);

                var collectionEntryIds = collectionEntries.Select(a => a.FursuitParticipationId);

                var fursuitParticipations = _appDbContext.FursuitParticipations
                    .Where(entity => collectionEntryIds.Contains(entity.Id));

                var fursuitParticipationIds = fursuitParticipations.Select(a => a.FursuitBadgeId);

                var fursuitBadges =
                    _appDbContext.FursuitBadges.Where(entity => fursuitParticipationIds.Contains(entity.Id));

                return collectionEntries.Select(a =>
                    {
                        var fursuitParticipation =
                            fursuitParticipations.Single(b => b.Id == a.FursuitParticipationId);
                        var fursuitBadge =
                            fursuitBadges.Single(b => b.Id == fursuitParticipation.FursuitBadgeId);

                        return new PlayerCollectionEntry()
                        {
                            BadgeId = fursuitBadge.Id,
                            Name = fursuitBadge.Name,
                            Species = fursuitBadge.Species,
                            Gender = fursuitBadge.Gender,
                            CollectionCount = fursuitParticipation.CollectionCount,
                            CollectedAtDateTimeUtc = a.EventDateTimeUtc
                        };
                    })
                    .OrderByDescending(a => a.CollectedAtDateTimeUtc)
                    .ToArray();
            }
        }

        public async Task<IResult> RegisterTokenForFursuitBadgeForOwnerAsync(string ownerUid, Guid fursuitBadgeId,
            string tokenValue)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: RegisterTokenForFursuitBadgeForOwnerAsync({ownerUid}, {fursuitBadgeId},  {tokenValue}): {time} ms",
                       ownerUid, fursuitBadgeId, tokenValue, time.TotalMilliseconds)))
            {
                try
                {
                    await _semaphore.WaitAsync();

                    var fursuitBadge = await _appDbContext.FursuitBadges.FirstOrDefaultAsync(
                        badge => badge.OwnerUid == ownerUid && badge.Id == fursuitBadgeId);
                    if (fursuitBadge == null)
                    {
                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Failed RegisterTokenForFursuitBadgeForOwnerAsync for {ownerUid}: {reason}", ownerUid,
                            "INVALID_FURSUIT_BADGE_ID");
                        return Result.Error("INVALID_FURSUIT_BADGE_ID", "Invalid fursuitBadgeId");
                    }

                    var token = await _appDbContext.Tokens.FirstOrDefaultAsync(t =>
                        t.Value == tokenValue && t.IsLinked == false);
                    if (token == null)
                    {
                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Failed RegisterTokenForFursuitBadgeForOwnerAsync for {ownerUid}: {reason}", ownerUid,
                            "INVALID_TOKEN");
                        return Result.Error("INVALID_TOKEN", "Invalid token");
                    }

                    var existingParticipation =
                        await _appDbContext.FursuitParticipations.FirstOrDefaultAsync(p =>
                            p.FursuitBadgeId == fursuitBadgeId);
                    if (existingParticipation != null)
                    {
                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Failed RegisterTokenForFursuitBadgeForOwnerAsync for {ownerUid}: {reason}", ownerUid,
                            "EXISTING_PARTICIPATION");
                        return Result.Error("EXISTING_PARTICIPATION", "Fursuit already has a token assigned to it");
                    }

                    var newParticipation = new FursuitParticipationRecord()
                    {
                        Id = Guid.NewGuid(),
                        FursuitBadgeId = fursuitBadgeId,
                        TokenValue = token.Value,
                        TokenRegistrationDateTimeUtc = DateTime.UtcNow,
                        CollectionCount = 0,
                        OwnerUid = ownerUid
                    };

                    token.IsLinked = true;
                    token.LinkedFursuitParticipantUid = newParticipation.Id;
                    token.LinkDateTimeUtc = DateTime.UtcNow;

                    _appDbContext.Tokens.Update(token);
                    _appDbContext.FursuitParticipations.Add(newParticipation);

                    await _appDbContext.SaveChangesAsync();

                    _logger.LogInformation(
                        LogEvents.CollectionGame,
                        "Successful RegisterTokenForFursuitBadgeForOwnerAsync for {ownerUid}: {fursuitName} now has token {tokenValue}",
                        ownerUid, fursuitBadge.Name, token.Value);

                    return Result.Ok;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public async Task<IResult<CollectTokenResponse>> CollectTokenForPlayerAsync(string playerUid, string tokenValue)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: CollectTokenForPlayerAsync({playerUid}, {tokenValue}): {time} ms",
                       playerUid, tokenValue, time.TotalMilliseconds)))
            {
                if (string.IsNullOrWhiteSpace(tokenValue))
                {
                    _logger.LogTrace(LogEvents.CollectionGame,
                        "Rejected CollectTokenForPlayerAsync (empty token) for player {playerUid}", playerUid);
                    return Result<CollectTokenResponse>.Error("EMPTY_TOKEN", "Token cannot be empty.");
                }

                try
                {
                    await _semaphore.WaitAsync();

                    var playerParticipation =
                        await _appDbContext.PlayerParticipations
                            .Include(playerParticipationRecord => playerParticipationRecord.CollectionEntries)
                            .FirstOrDefaultAsync(a => a.PlayerUid == playerUid);
                    if (playerParticipation == null)
                    {
                        playerParticipation = new PlayerParticipationRecord()
                        {
                            Id = Guid.NewGuid(),
                            PlayerUid = playerUid,
                            Karma = 0
                        };

                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Creating initial PlayerParticipationRecord for {playerUid}", playerUid);
                        _appDbContext.PlayerParticipations.Add(playerParticipation);
                        await _appDbContext.SaveChangesAsync();
                    }

                    if (playerParticipation.IsBanned)
                    {
                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Rejected CollectTokenForPlayerAsync for banned player {playerUid}", playerUid);
                        return Result<CollectTokenResponse>.Error("BANNED",
                            "You have been disqualified from the game.");
                    }

                    var fursuitParticipation =
                        await _appDbContext.FursuitParticipations
                            .Include(fursuitParticipationRecord => fursuitParticipationRecord.CollectionEntries)
                            .FirstOrDefaultAsync(a => a.TokenValue == tokenValue && a.IsBanned == false);

                    if (fursuitParticipation == null)
                    {
                        playerParticipation.Karma -= 1;
                        if (playerParticipation.Karma <= -10)
                        {
                            playerParticipation.IsBanned = true;
                        }

                        _appDbContext.PlayerParticipations.Update(playerParticipation);
                        await _appDbContext.SaveChangesAsync();

                        var sb = new StringBuilder("The token you specified is not valid.");

                        if (playerParticipation.Karma < -7)
                        {
                            if (playerParticipation.Karma > -10)
                            {
                                sb.Append(
                                    $" Careful - you will be disqualified after {playerParticipation.Karma + 10} more failed attempts.");
                                _logger.LogInformation(LogEvents.CollectionGame,
                                    "Failed CollectTokenForPlayerAsync for {playerUid} using token {tokenValue}: {reason}",
                                    playerUid, tokenValue, "INVALID_TOKEN_BAN_IMMINENT");
                                return Result<CollectTokenResponse>.Error("INVALID_TOKEN_BAN_IMMINENT", sb.ToString());
                            }
                            else
                            {
                                var identity =
                                    await _appDbContext.RegSysIdentities.FirstOrDefaultAsync(a =>
                                        a.Uid == playerParticipation.PlayerUid);

                                await SendToTelegramManagementChannelAsync(
                                    $"*Player Banned:*\n{playerParticipation.PlayerUid} ({identity.Username})\n(Had {playerParticipation.CollectionCount} codes successfully collected so far.)");

                                sb.Append(" You have been disqualified.");
                                _logger.LogWarning(LogEvents.CollectionGame,
                                    "Failed CollectTokenForPlayerAsync for {playerUid} using token {tokenValue}: {reason}",
                                    playerUid, tokenValue, "INVALID_TOKEN_BANNED");
                                return Result<CollectTokenResponse>.Error("INVALID_TOKEN_BANNED", sb.ToString());
                            }
                        }

                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Failed CollectTokenForPlayerAsync for {playerUid} using token {tokenValue}: {reason}",
                            playerUid, tokenValue, "INVALID_TOKEN");
                        return Result<CollectTokenResponse>.Error("INVALID_TOKEN", sb.ToString());
                    }

                    if (playerParticipation.PlayerUid == fursuitParticipation.OwnerUid)
                    {
                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Failed CollectTokenForPlayerAsync for {playerUid} using token {tokenValue}: {reason}",
                            playerUid, tokenValue, "INVALID_TOKEN_OWN_SUIT");
                        return Result<CollectTokenResponse>.Error("INVALID_TOKEN_OWN_SUIT",
                            "You cannot collect your own fursuits.");
                    }

                    if (playerParticipation.CollectionEntries.Any(
                            a => a.FursuitParticipationId == fursuitParticipation.Id))
                    {
                        _logger.LogDebug(LogEvents.CollectionGame,
                            "Failed CollectTokenForPlayerAsync for {playerUid} using token {tokenValue}: {reason}",
                            playerUid, tokenValue, "INVALID_TOKEN_ALREADY_COLLECTED");
                        return Result<CollectTokenResponse>.Error("INVALID_TOKEN_ALREADY_COLLECTED",
                            "You have already collected this suit!");
                    }

                    playerParticipation.Karma = Math.Min(playerParticipation.Karma + 2, 10);

                    playerParticipation.CollectionEntries.Add(new CollectionEntry()
                    {
                        EventDateTimeUtc = DateTime.UtcNow,
                        FursuitParticipationId = fursuitParticipation.Id
                    });

                    playerParticipation.LastCollectionDateTimeUtc = DateTime.UtcNow;
                    playerParticipation.CollectionCount = playerParticipation.CollectionEntries.Count;
                    _appDbContext.PlayerParticipations.Update(playerParticipation);

                    await _appDbContext.SaveChangesAsync();

                    // This should never *not* happen, but makes testing a bit easier.
                    if (fursuitParticipation.CollectionEntries.All(a => a.PlayerParticipationId != playerUid))
                    {
                        fursuitParticipation.CollectionEntries.Add(new CollectionEntry()
                        {
                            EventDateTimeUtc = DateTime.UtcNow,
                            PlayerParticipationId = playerUid
                        });

                        fursuitParticipation.LastCollectionDateTimeUtc = DateTime.UtcNow;
                        fursuitParticipation.CollectionCount = fursuitParticipation.CollectionEntries.Count;
                        _appDbContext.FursuitParticipations.Update(fursuitParticipation);
                        await _appDbContext.SaveChangesAsync();
                    }

                    var fursuitBadge = await
                        _appDbContext.FursuitBadges.FirstOrDefaultAsync(
                            a => a.Id == fursuitParticipation.FursuitBadgeId);

                    _logger.LogInformation(
                        LogEvents.CollectionGame,
                        "Successful CollectTokenForPlayerAsync for {playerUid} using token {tokenValue} (now has {playerCollectionCount} catches): {fursuitName} ({fursuitCollectionCount} times caught)",
                        playerUid, tokenValue, playerParticipation.CollectionCount, fursuitBadge.Name,
                        fursuitParticipation.CollectionCount);

                    return Result<CollectTokenResponse>.Ok(new CollectTokenResponse()
                    {
                        FursuitBadgeId = fursuitBadge.Id,
                        Name = fursuitBadge.Name,
                        Gender = fursuitBadge.Gender,
                        Species = fursuitBadge.Species,
                        FursuitCollectionCount = fursuitParticipation.CollectionCount
                    });
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        public async Task<IResult<PlayerScoreboardEntry[]>> GetPlayerScoreboardEntriesAsync(int top)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: GetPlayerScoreboardEntriesAsync({top}): {time} ms",
                       top, time.TotalMilliseconds)))
            {
                var topPlayers = _appDbContext.PlayerParticipations.Where(
                        a => !a.IsBanned && a.IsDeleted == 0 && a.CollectionCount > 0
                    ).OrderByDescending(a => a.CollectionCount)
                    .ThenBy(a => a.LastCollectionDateTimeUtc)
                    .Take(top);

                var topPlayersUids = topPlayers.Select(a => a.PlayerUid);
                var identities = _appDbContext.RegSysIdentities.Where(a => topPlayersUids.Contains(a.Uid));

                var result = await topPlayers
                    .Select(a => new PlayerScoreboardEntry()
                    {
                        Name = identities.SingleOrDefault(b => b.Uid == a.PlayerUid).Username ?? "(?)",
                        CollectionCount = a.CollectionCount,
                    })
                    .ToArrayAsync();

                for (var i = 0; i < result.Length; i++) result[i].Rank = i + 1;
                return Result<PlayerScoreboardEntry[]>.Ok(result);
            }
        }

        public async Task<IResult<FursuitScoreboardEntry[]>> GetFursuitScoreboardEntriesAsync(int top)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: GetFursuitScoreboardEntriesAsync({top}): {time} ms",
                       top, time.TotalMilliseconds)))
            {
                var topFursuits = await _appDbContext.FursuitParticipations.Where(
                        a => !a.IsBanned && a.IsDeleted == 0 && a.CollectionCount > 0)
                    .OrderByDescending(a => a.CollectionCount)
                    .ThenBy(a => a.LastCollectionDateTimeUtc)
                    .Take(top)
                    .ToListAsync();

                var topFursuitBadgeIds = topFursuits.Select(a => a.FursuitBadgeId);

                var fursuitBadges = _appDbContext.FursuitBadges.Where(entity => topFursuitBadgeIds.Contains(entity.Id));

                var result = topFursuits
                    .Select(entry =>
                    {
                        var fursuitBadge = fursuitBadges.Single(badge => badge.Id == entry.FursuitBadgeId);
                        return new FursuitScoreboardEntry()
                        {
                            Name = fursuitBadge.Name,
                            CollectionCount = entry.CollectionCount,
                            Gender = fursuitBadge.Gender,
                            Species = fursuitBadge.Species,
                            BadgeId = fursuitBadge.Id
                        };
                    })
                    .ToArray();

                for (var i = 0; i < result.Length; i++) result[i].Rank = i + 1;
                return Result<FursuitScoreboardEntry[]>.Ok(result);
            }
        }

        public async Task<IResult> CreateTokenFromValueAsync(string tokenValue)
        {
            if (string.IsNullOrWhiteSpace(tokenValue))
                return Result.Error("TOKEN_EMPTY", "Token cannot be empty");

            var tokenRecord = new TokenRecord
            {
                Id = Guid.NewGuid(),
                LastChangeDateTimeUtc = DateTime.UtcNow,
                Value = tokenValue
            };

            try
            {
                await _semaphore.WaitAsync();
                _appDbContext.Tokens.Add(tokenRecord);
                await _appDbContext.SaveChangesAsync();

                return Result.Ok;
            }
            catch (Exception ex)
            {
                return Result.Error("EXCEPTION", ex.Message);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IResult> CreateTokensFromValuesAsync(string[] tokenValues)
        {
            foreach (var tokenValue in tokenValues)
            {
                var result = await CreateTokenFromValueAsync(tokenValue);
                if (!result.IsSuccessful) return result;
            }

            return Result.Ok;
        }

        public async Task<IResult> UnbanPlayerAsync(string playerUid)
        {
            using (new TimeTrap(time => _logger.LogTrace(
                       LogEvents.CollectionGame,
                       "Benchmark: RegisterTokenForFursuitBadgeForOwnerAsync({playerUid}): {time} ms",
                       playerUid, time.TotalMilliseconds)))
            {
                try
                {
                    await _semaphore.WaitAsync();

                    var playerParticipation =
                        await _appDbContext.PlayerParticipations.FirstOrDefaultAsync(a => a.PlayerUid == playerUid);

                    if (playerParticipation == null)
                        return Result.Error("INVALID_PLAYERUID", $"No player found with uid = {playerUid}");

                    if (playerParticipation.IsBanned == false)
                        return Result.Error("NOT_BANNED", $"Player with uid {playerUid} is not banned.");

                    playerParticipation.IsBanned = false;
                    playerParticipation.Karma = 0;
                    playerParticipation.Touch();

                    _appDbContext.PlayerParticipations.Update(playerParticipation);
                    await _appDbContext.SaveChangesAsync();
                    await SendToTelegramManagementChannelAsync($"*Player Unbanned*: {playerParticipation.PlayerUid}");

                    return Result.Ok;
                }
                finally
                {
                    _semaphore.Release();
                }
            }
        }

        private async Task SendToTelegramManagementChannelAsync(string message)
        {
            if (string.IsNullOrWhiteSpace(_collectionGameConfiguration.TelegramManagementChatId)) return;

            await _telegramMessageSender.SendMarkdownMessageToChatAsync(
                _collectionGameConfiguration.TelegramManagementChatId, message);
        }

        public async Task<IResult> RecalculateAsync()
        {
            await _semaphore.WaitAsync();

            try
            {
                var playerParticipations = _appDbContext.PlayerParticipations;

                foreach (var playerParticipation in playerParticipations)
                {
                    playerParticipation.CollectionCount = playerParticipation.CollectionEntries?.Count() ?? 0;
                    playerParticipation.LastCollectionDateTimeUtc =
                        (playerParticipation.CollectionCount == 0)
                            ? (DateTime?)null
                            : playerParticipation.CollectionEntries.Max(a => a.EventDateTimeUtc);

                    _appDbContext.PlayerParticipations.Update(playerParticipation);
                }

                var fursuitParticipations = _appDbContext.FursuitParticipations;

                foreach (var fursuitParticipation in fursuitParticipations)
                {
                    fursuitParticipation.CollectionCount = fursuitParticipation.CollectionEntries?.Count() ?? 0;
                    fursuitParticipation.LastCollectionDateTimeUtc =
                        (fursuitParticipation.CollectionCount == 0)
                            ? (DateTime?)null
                            : fursuitParticipation.CollectionEntries.Max(a => a.EventDateTimeUtc);

                    _appDbContext.FursuitParticipations.Update(fursuitParticipation);
                }

                await _appDbContext.SaveChangesAsync();

                return Result.Ok;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task UpdateFursuitParticipationAsync()
        {
            try
            {
                await _semaphore.WaitAsync();

                var participatingFursuitBadges = _appDbContext.FursuitBadges.Where(badge => !string.IsNullOrEmpty(badge.CollectionCode));
                var fursuitParticipationRecords = _appDbContext.FursuitParticipations;

                var toJoin = participatingFursuitBadges
                    .Where(badge => !fursuitParticipationRecords.Any(fpr => fpr.FursuitBadgeId == badge.Id)).ToList();
                var toVerify = fursuitParticipationRecords.ToList();


                foreach (var badge in toJoin)
                {
                    var newParticipation = new FursuitParticipationRecord()
                    {
                        Id = Guid.NewGuid(),
                        FursuitBadgeId = badge.Id,
                        TokenValue = badge.CollectionCode,
                        TokenRegistrationDateTimeUtc = DateTime.UtcNow,
                        CollectionCount = 0,
                        OwnerUid = badge.OwnerUid
                    };

                    _appDbContext.FursuitParticipations.Add(newParticipation);
                }

                foreach (var existingParticipation in toVerify)
                {
                    var badge = participatingFursuitBadges.SingleOrDefault(badge =>
                        badge.Id == existingParticipation.FursuitBadgeId);

                    // Badge exists, collection code is the same? Move on.
                    if (badge != null && existingParticipation.TokenValue == badge.CollectionCode) continue;

                    // Badge exists, collection code has changed? Update it & move on.
                    if (badge != null && existingParticipation.TokenValue != badge.CollectionCode)
                    {
                        existingParticipation.TokenValue = badge.CollectionCode;
                        existingParticipation.IsBanned = false;
                        _appDbContext.FursuitParticipations.Update(existingParticipation);
                        continue;
                    }

                    // No or non-participating badge and already banned? Move on.
                    if (badge == null && existingParticipation.IsBanned) continue;

                    // We have no badge (=not existing, or no more collection code) - ban the participation.
                    if (badge == null && !existingParticipation.IsBanned)
                    {
                        existingParticipation.IsBanned = true;
                        _appDbContext.FursuitParticipations.Update(existingParticipation);
                        continue;
                    }
                }

                await _appDbContext.SaveChangesAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
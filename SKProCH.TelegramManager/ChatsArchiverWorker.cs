using SKProCH.TelegramManager.Configuration;
using TL;
using WTelegram;

namespace SKProCH.TelegramManager;

public class ChatsArchiverWorker : BackgroundService {
    private readonly ILogger<ChatsArchiverWorker> _logger;
    private readonly Client _telegram;
    private readonly ChatsArchiverConfigurationSection _archiverConfiguration;
    private Dictionary<long, InputPeer> _mutedPeers = new();
    private Timer? _updateMutedPeersTimer;

    public ChatsArchiverWorker(ILogger<ChatsArchiverWorker> logger, Client telegram, ChatsArchiverConfigurationSection archiverConfiguration) {
        _logger = logger;
        _telegram = telegram;
        _archiverConfiguration = archiverConfiguration;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
        => Task.CompletedTask;

    public override async Task StartAsync(CancellationToken cancellationToken) {
        if (!_archiverConfiguration.Enabled) return;
        
        var updateMutedPeers = await UpdateMutedPeers();
        await MutePeer(updateMutedPeers.Keys.ToArray());
        _updateMutedPeersTimer = new Timer(state => _ = UpdateMutedPeers(), null, _archiverConfiguration.AchieveUpdatePeriod, _archiverConfiguration.AchieveUpdatePeriod);

        _telegram.OnUpdate += OnUpdateReceived;
        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken) {
        await _updateMutedPeersTimer.DisposeAsync();
        _telegram.OnUpdate -= OnUpdateReceived;
        await base.StopAsync(cancellationToken);
    }

    private async Task<Dictionary<long, InputPeer>> UpdateMutedPeers() {
        _logger.LogInformation("Starting muted peers discovering");
        var messagesGetAllDialogs = await _telegram.Messages_GetDialogFilters();
        var mutedFolders = messagesGetAllDialogs
            .Where(filter => filter != null)
            .Where(filter => filter.title != null && filter.title.StartsWith('[') && filter.title.EndsWith(']'))
            .ToArray();
        _logger.LogInformation("Found {foldersCount} muted folders: {folders}", mutedFolders.Length, mutedFolders);

        _mutedPeers = mutedFolders.SelectMany(filter => filter.include_peers)
            .ToDictionary(GetIdFromGroupPeer);
        _logger.LogInformation("Found {dialogsCount} dialogs to mute", _mutedPeers.Count);
        return _mutedPeers;

        static long GetIdFromGroupPeer(InputPeer peer) {
            return peer switch {
                InputPeerChannel inputPeerChannel => inputPeerChannel.channel_id,
                InputPeerChat inputPeerChat       => inputPeerChat.chat_id,
                InputPeerUser inputPeerUser       => inputPeerUser.user_id,
                _                                 => throw new ArgumentOutOfRangeException(nameof(peer))
            };
        }
    }

    private async Task OnUpdateReceived(IObject arg) {
        var id = arg switch {
            UpdateNewChannelMessage updateNewChannelMessage => updateNewChannelMessage.message.Peer.ID,
            UpdateNewMessage updateNewMessage               => updateNewMessage.message.Peer.ID,
            UpdateShortChatMessage updateShortChatMessage   => updateShortChatMessage.chat_id,
            UpdateShortMessage updateShortMessage           => updateShortMessage.flags == UpdateShortMessage.Flags.out_ ? null : updateShortMessage.user_id,
            _                                               => (long?)null
        };
        if (id == null) return;
        _logger.LogInformation("Received new message from peer {peerId}", id);
        if (_mutedPeers.TryGetValue(id.Value, out _)) {
            await MutePeer(id.Value);
        }
    }

    private async Task MutePeer(params long[] peerIds) {
        _logger.LogInformation("Requested mute for {peersCount} peers: {peers}", peerIds.Length, peerIds);
        var inputPeers = peerIds.Select(l => _mutedPeers[l])
            .Select(peer => new InputFolderPeer() { folder_id = 1, peer = peer });
        await _telegram.Folders_EditPeerFolders(inputPeers.ToArray());
        _logger.LogInformation("Successfully muted {peersCount} peers", peerIds.Length);
    }
}
/// One (type, channel) pair the user has switched off.
///
/// The pair is the identity, not the type. Mutes used to be a flat list of type
/// names that silenced the type on every channel at once, so "don't text me
/// about messages" and "don't show me messages" were one setting and no
/// per-channel choice was expressible.
class NotificationMute {
  final String type;
  final String channel;

  const NotificationMute({required this.type, required this.channel});

  factory NotificationMute.fromJson(Map<String, dynamic> json) => NotificationMute(
        type: (json['type'] ?? '').toString(),
        // An empty channel predates the column and meant "everywhere"; the
        // server expands those before sending, so this is only a fallback.
        channel: (json['channel'] ?? 'in_app').toString(),
      );

  Map<String, dynamic> toJson() => {'type': type, 'channel': channel};

  @override
  bool operator ==(Object other) =>
      other is NotificationMute && other.type == type && other.channel == channel;

  @override
  int get hashCode => Object.hash(type, channel);
}

/// A channel the user can configure, as the server scopes it for this role.
///
/// SMS and push are freelancer-only, so a client or admin never receives these
/// in the first place and cannot render a switch for them.
class NotificationChannelOption {
  final String key;
  final String label;
  final String description;
  final bool enabled;
  final bool defaultEnabled;

  const NotificationChannelOption({
    required this.key,
    required this.label,
    required this.description,
    required this.enabled,
    required this.defaultEnabled,
  });

  factory NotificationChannelOption.fromJson(Map<String, dynamic> json) =>
      NotificationChannelOption(
        key: (json['key'] ?? '').toString(),
        label: (json['label'] ?? '').toString(),
        description: (json['description'] ?? '').toString(),
        enabled: json['enabled'] != false,
        defaultEnabled: json['default_enabled'] != false,
      );

  /// Icon key for the UI. Presentation only; enforcement is the server's.
  String get iconKey => switch (key) {
        'in_app' => 'in_app',
        'email' => 'email',
        'sms' => 'sms',
        'push' => 'push',
        _ => 'in_app',
      };
}

/// A notification type, and the channels it can travel over.
class NotificationTypeOption {
  final String type;
  final String label;
  final String description;
  final List<String> channels;

  const NotificationTypeOption({
    required this.type,
    required this.label,
    required this.description,
    required this.channels,
  });

  factory NotificationTypeOption.fromJson(Map<String, dynamic> json) =>
      NotificationTypeOption(
        type: (json['type'] ?? '').toString(),
        label: (json['label'] ?? '').toString(),
        description: (json['description'] ?? '').toString(),
        channels: (json['channels'] as List?)?.map((e) => e.toString()).toList() ?? const [],
      );
}

/// The user's notification delivery switches, plus the per-type/per-channel grid.
///
/// ## Defaults come from the server, not from here
///
/// This used to hardcode "all on" in the constructor and justify it by pointing
/// at the server's missing-row behaviour. That argument no longer holds: the
/// server now resolves each channel against `NotificationCatalogue.DefaultFor`,
/// where **SMS is off** because it costs money per message. Hardcoding SMS on
/// would show a user a live switch the server is ignoring -- the exact failure
/// this screen is meant to stop.
///
/// So the server sends the resolved values and this model only parses them. The
/// constructor defaults exist for a not-yet-loaded view, not as a policy.
class NotificationPreferences {
  /// Master switch. Off silences every channel while keeping the individual
  /// choices underneath, so re-enabling restores them.
  final bool masterEnabled;

  /// The notification row plus the WebSocket push.
  final bool inAppEnabled;

  /// FCM, for when the app process is not running.
  final bool pushEnabled;

  /// Whether the *server* has working FCM credentials.
  ///
  /// Distinct from [pushEnabled], which is the user's preference. When this is
  /// false the server accepts the switch and then delivers nothing, so the UI has
  /// to say so rather than leaving the user with a toggle that silently does
  /// nothing.
  final bool pushConfigured;

  /// Off by default. See [NotificationCatalogue.DefaultFor] on the server.
  final bool smsEnabled;

  /// Business-notification email.
  final bool emailEnabled;

  final List<NotificationChannelOption> channels;
  final List<NotificationTypeOption> types;
  final List<NotificationMute> muted;

  const NotificationPreferences({
    this.masterEnabled = true,
    this.inAppEnabled = true,
    this.pushEnabled = true,
    this.pushConfigured = true,
    this.smsEnabled = false,
    this.emailEnabled = true,
    this.channels = const [],
    this.types = const [],
    this.muted = const [],
  });

  factory NotificationPreferences.fromJson(Map<String, dynamic> json) {
    final rawMuted = (json['muted'] as List?) ?? const [];
    return NotificationPreferences(
      masterEnabled: json['master_enabled'] != false,
      inAppEnabled: json['in_app_enabled'] != false,
      pushEnabled: json['push_enabled'] != false,
      pushConfigured: json['push_configured'] != false,
      // The field is absent on a server that predates email, but the channel
      // list is authoritative when present, so prefer it.
      smsEnabled: json['sms_enabled'] != false,
      emailEnabled: json['email_enabled'] != false,
      channels: (json['channels'] as List?)
              ?.map((e) => NotificationChannelOption.fromJson(
                  Map<String, dynamic>.from(e as Map)))
              .toList() ??
          const [],
      types: (json['types'] as List?)
              ?.map((e) => NotificationTypeOption.fromJson(
                  Map<String, dynamic>.from(e as Map)))
              .toList() ??
          const [],
      muted: rawMuted
          .map((e) => NotificationMute.fromJson(Map<String, dynamic>.from(e as Map)))
          .toList(),
    );
  }

  /// Whether this (type, channel) cell is muted.
  bool isMuted(String type, String channel) =>
      muted.any((m) => m.type == type && m.channel == channel);

  /// Whether the channel itself is on, ignoring mutes.
  ///
  /// The master switch folds in: turning everything off has to make every
  /// channel read as off, or the screen shows channels that are on while nothing
  /// is delivered.
  bool isChannelOn(String channelKey) {
    if (!masterEnabled) return false;
    switch (channelKey) {
      case 'in_app':
        return inAppEnabled;
      case 'email':
        return emailEnabled;
      case 'sms':
        return smsEnabled;
      case 'push':
        return pushEnabled;
      default:
        return false;
    }
  }

  /// A copy with some fields replaced.
  ///
  /// Every switch in the settings screen produces one of these rather than
  /// mutating in place, so a failed write can be rolled back by restoring the
  /// previous object -- a switch that stays on after the server refused it is
  /// worse than one that visibly snaps back.
  ///
  /// [channels] and [types] default to `this` rather than to a literal empty
  /// list, so a copy that flips one switch cannot accidentally drop the
  /// vocabulary the screen renders from.
  NotificationPreferences copyWith({
    bool? masterEnabled,
    bool? inAppEnabled,
    bool? pushEnabled,
    bool? pushConfigured,
    bool? smsEnabled,
    bool? emailEnabled,
    List<NotificationChannelOption>? channels,
    List<NotificationTypeOption>? types,
    List<NotificationMute>? muted,
  }) =>
      NotificationPreferences(
        masterEnabled: masterEnabled ?? this.masterEnabled,
        inAppEnabled: inAppEnabled ?? this.inAppEnabled,
        pushEnabled: pushEnabled ?? this.pushEnabled,
        pushConfigured: pushConfigured ?? this.pushConfigured,
        smsEnabled: smsEnabled ?? this.smsEnabled,
        emailEnabled: emailEnabled ?? this.emailEnabled,
        channels: channels ?? this.channels,
        types: types ?? this.types,
        muted: muted ?? this.muted,
      );

  /// Toggles one cell and returns the resulting list.
  ///
  /// The caller sends the whole list back, because the server treats `muted` as a
  /// replacement rather than a delta. A merge would mean a user could never
  /// un-mute anything.
  List<NotificationMute> toggle(String type, String channel) {
    final next = List<NotificationMute>.from(muted);
    final target = NotificationMute(type: type, channel: channel);
    if (!next.remove(target)) next.add(target);
    return next;
  }

  /// The types shown under a channel, in the server's order.
  List<NotificationTypeOption> typesFor(String channelKey) =>
      types.where((t) => t.channels.contains(channelKey)).toList();
}

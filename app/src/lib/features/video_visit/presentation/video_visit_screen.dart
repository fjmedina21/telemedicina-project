import 'package:flutter/material.dart';
import 'package:livekit_client/livekit_client.dart';

import '../../../core/network/api_client.dart';
import '../../../core/models/json_map.dart';

class VideoVisitScreen extends StatefulWidget {
  final ApiClient api;
  final String appointmentId;
  final String practitionerName;
  const VideoVisitScreen({super.key, required this.api, required this.appointmentId,
    required this.practitionerName});
  @override State<VideoVisitScreen> createState() => _VideoVisitScreenState();
}

class _VideoVisitScreenState extends State<VideoVisitScreen> {
  Room? room;
  EventsListener<RoomEvent>? listener;
  JsonMap? session;
  String? error;
  bool loading = true, ending = false;
  bool micMuted = false, cameraOff = false;

  @override
  void initState() { super.initState(); _connect(); }

  Future<void> _connect() async {
    try {
      final token = await widget.api.request('POST',
        '/appointments/${Uri.encodeComponent(widget.appointmentId)}/video-token',
        extraHeaders: {'Idempotency-Key': ApiClient.uuid()}) as Map;
      session = Map<String, dynamic>.from(token);
      final nextRoom = Room(roomOptions: const RoomOptions(adaptiveStream: true, dynacast: true));
      room = nextRoom;
      listener = nextRoom.createListener();
      listener!.on<RoomConnectedEvent>((_) => _event('connected'));
      listener!.on<RoomReconnectingEvent>((_) => _event('reconnecting'));
      listener!.on<RoomReconnectedEvent>((_) => _event('reconnected'));
      listener!.on<RoomDisconnectedEvent>((_) => _event('disconnected'));
      await nextRoom.connect(token['url'] as String, token['token'] as String);
      final local = nextRoom.localParticipant;
      if (local == null) throw StateError('LiveKit local participant unavailable.');
      await local.setCameraEnabled(true);
      await local.setMicrophoneEnabled(true);
      if (mounted) setState(() => loading = false);
    } catch (_) {
      if (mounted) setState(() { loading = false; error = 'No se pudo conectar a la videoconsulta.'; });
    }
  }

  Future<void> _event(String type) async {
    final id = session?['sessionId'] as String?;
    if (id == null) return;
    try {
      await widget.api.request('POST', '/video-sessions/${id}/connection-events',
        body: {'type': type});
    } catch (_) {}
    if (mounted) setState(() {});
  }

  Future<void> _toggleMic() async {
    final value = !micMuted;
    await room?.localParticipant?.setMicrophoneEnabled(!value);
    if (mounted) setState(() => micMuted = value);
  }

  Future<void> _toggleCamera() async {
    final value = !cameraOff;
    await room?.localParticipant?.setCameraEnabled(!value);
    if (mounted) setState(() => cameraOff = value);
  }

  Future<void> _leave() async {
    if (ending) return;
    setState(() => ending = true);
    await _event('disconnected');
    await room?.disconnect();
    if (mounted) Navigator.pop(context);
  }

  @override
  void dispose() {
    listener?.dispose();
    room?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text('Videoconsulta · ${widget.practitionerName}')),
      body: loading ? const Center(child: CircularProgressIndicator()) :
        error != null ? Center(child: Column(mainAxisSize: MainAxisSize.min, children: [
          Text(error!), const SizedBox(height: 12),
          FilledButton(onPressed: () { setState(() { loading = true; error = null; }); _connect(); },
            child: const Text('Reintentar')),
        ])) : room == null ? const SizedBox.shrink() :
        Column(children: [
          Expanded(child: _ParticipantGrid(room: room!)),
          Padding(padding: const EdgeInsets.all(16), child: Row(
            mainAxisAlignment: MainAxisAlignment.center, children: [
              IconButton(onPressed: _toggleMic, icon: Icon(micMuted ? Icons.mic_off : Icons.mic)),
              IconButton(onPressed: _toggleCamera, icon: Icon(cameraOff ? Icons.videocam_off : Icons.videocam)),
              const SizedBox(width: 20),
              FilledButton.tonal(onPressed: ending ? null : _leave,
                child: const Text('Salir de la sala')),
            ])),
        ]),
    );
  }
}

class _ParticipantGrid extends StatelessWidget {
  final Room room;
  const _ParticipantGrid({required this.room});
  @override
  Widget build(BuildContext context) {
    final participants = <Participant<dynamic>>[
      if (room.localParticipant != null) room.localParticipant!,
      ...room.remoteParticipants.values,
    ];
    return GridView.builder(
      padding: const EdgeInsets.all(12),
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 2, crossAxisSpacing: 8, mainAxisSpacing: 8),
      itemCount: participants.length,
      itemBuilder: (_, index) => _ParticipantTile(participant: participants[index]),
    );
  }
}

class _ParticipantTile extends StatelessWidget {
  final Participant<dynamic> participant;
  const _ParticipantTile({required this.participant});
  @override
  Widget build(BuildContext context) {
    VideoTrack? track;
    for (final publication in participant.videoTrackPublications) {
      if (publication.track is VideoTrack && !publication.isScreenShare) {
        track = publication.track as VideoTrack; break;
      }
    }
    return DecoratedBox(
      decoration: BoxDecoration(color: Colors.black, borderRadius: BorderRadius.circular(12)),
      child: track == null ? Center(child: Text(participant.name.isEmpty ? 'Participante' : participant.name,
        style: const TextStyle(color: Colors.white))) : VideoTrackRenderer(track),
    );
  }
}

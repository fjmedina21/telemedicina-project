import 'package:flutter/material.dart';
import 'package:telemedicina_app/core/models/appointment.dart';

class VideoVisitScreen extends StatefulWidget {
  final Appointment appointment;

  const VideoVisitScreen({super.key, required this.appointment});

  @override
  State<VideoVisitScreen> createState() => _VideoVisitScreenState();
}

class _VideoVisitScreenState extends State<VideoVisitScreen> {
  bool _isMicMuted = false;
  bool _isCameraOff = false;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Colors.black,
      appBar: AppBar(
        backgroundColor: Colors.black,
        foregroundColor: Colors.white,
        title: Text(
          widget.appointment.doctorName,
          style: const TextStyle(fontSize: 16),
        ),
        actions: [
          Container(
            margin: const EdgeInsets.only(right: 16),
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
            decoration: BoxDecoration(
              color: Colors.red.shade600,
              borderRadius: BorderRadius.circular(12),
            ),
            child: const Text(
              'EN VIVO',
              style: TextStyle(
                fontSize: 10,
                fontWeight: FontWeight.bold,
                color: Colors.white,
              ),
            ),
          ),
        ],
      ),
      body: SafeArea(
        child: Column(
          children: [
            // Vista principal de video
            Expanded(
              child: Stack(
                children: [
                  Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        CircleAvatar(
                          radius: 50,
                          backgroundColor: Colors.grey.shade800,
                          child: const Icon(
                            Icons.person,
                            size: 60,
                            color: Colors.white70,
                          ),
                        ),
                        const SizedBox(height: 16),
                        Text(
                          widget.appointment.doctorName,
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          widget.appointment.specialty,
                          style: const TextStyle(
                            color: Colors.white60,
                            fontSize: 13,
                          ),
                        ),
                        const SizedBox(height: 16),
                        Container(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 14,
                            vertical: 6,
                          ),
                          decoration: BoxDecoration(
                            color: Colors.teal.shade900.withValues(alpha: 0.8),
                            borderRadius: BorderRadius.circular(20),
                            border: Border.all(
                              color: Colors.teal.shade400,
                              width: 0.5,
                            ),
                          ),
                          child: const Text(
                            'Conexión Segura Cifrada de Extremo a Extremo',
                            style: TextStyle(
                              color: Colors.tealAccent,
                              fontSize: 11,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),

                  // Miniatura propia (PIP)
                  Positioned(
                    top: 16,
                    right: 16,
                    child: Container(
                      width: 100,
                      height: 140,
                      decoration: BoxDecoration(
                        color: Colors.grey.shade900,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: Colors.white24, width: 1.5),
                      ),
                      child: Center(
                        child: Text(
                          _isCameraOff ? 'Cámara apagada' : 'Tú',
                          style: const TextStyle(
                            color: Colors.white54,
                            fontSize: 11,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),

            // Barra de controles de la llamada
            Container(
              padding: const EdgeInsets.symmetric(vertical: 20, horizontal: 24),
              color: Colors.grey.shade900,
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                children: [
                  IconButton(
                    icon: Icon(
                      _isMicMuted ? Icons.mic_off_rounded : Icons.mic_rounded,
                      color: _isMicMuted ? Colors.redAccent : Colors.white,
                    ),
                    onPressed: () => setState(() => _isMicMuted = !_isMicMuted),
                  ),
                  IconButton(
                    icon: Icon(
                      _isCameraOff
                          ? Icons.videocam_off_rounded
                          : Icons.videocam_rounded,
                      color: _isCameraOff ? Colors.redAccent : Colors.white,
                    ),
                    onPressed: () =>
                        setState(() => _isCameraOff = !_isCameraOff),
                  ),
                  FloatingActionButton(
                    backgroundColor: Colors.red.shade600,
                    onPressed: () {
                      showDialog(
                        context: context,
                        builder: (ctx) => AlertDialog(
                          title: const Text('¿Finalizar llamada?'),
                          content: const Text(
                            'La consulta terminará y podrás consultar tus recetas en la app.',
                          ),
                          actions: [
                            TextButton(
                              onPressed: () => Navigator.pop(ctx),
                              child: const Text('Cancelar'),
                            ),
                            TextButton(
                              onPressed: () {
                                Navigator.pop(ctx);
                                Navigator.pop(context);
                              },
                              child: const Text(
                                'Salir',
                                style: TextStyle(color: Colors.red),
                              ),
                            ),
                          ],
                        ),
                      );
                    },
                    child: const Icon(
                      Icons.call_end_rounded,
                      color: Colors.white,
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

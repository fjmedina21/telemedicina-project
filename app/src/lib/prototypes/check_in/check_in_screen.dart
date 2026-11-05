import 'package:flutter/material.dart';
import 'package:telemedicina_app/app/routes.dart';
import 'package:telemedicina_app/core/models/appointment.dart';
import 'package:telemedicina_app/shared/widgets/custom_button.dart';

class CheckInScreen extends StatefulWidget {
  final Appointment appointment;

  const CheckInScreen({super.key, required this.appointment});

  @override
  State<CheckInScreen> createState() => _CheckInScreenState();
}

class _CheckInScreenState extends State<CheckInScreen> {
  bool _noEmergency = false;
  bool _consentAccepted = false;
  bool _identityVerified = false;
  bool _isSubmitting = false;

  void _proceedToVideo() async {
    if (!_noEmergency) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Debe confirmar que no se trata de una urgencia médica vital.',
          ),
        ),
      );
      return;
    }

    if (!_consentAccepted || !_identityVerified) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text(
            'Es obligatorio aceptar el consentimiento y verificar su identidad.',
          ),
        ),
      );
      return;
    }

    setState(() => _isSubmitting = true);
    await Future.delayed(const Duration(milliseconds: 600));

    if (!mounted) return;
    setState(() => _isSubmitting = false);

    Navigator.pushReplacementNamed(
      context,
      AppRoutes.videoVisit,
      arguments: widget.appointment,
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Admisión Virtual y Consentimiento')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Alerta médica de no urgencia
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: Colors.red.shade50,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: Colors.red.shade200),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.warning_amber_rounded,
                    color: Colors.red.shade700,
                    size: 24,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Aviso de Emergencias',
                          style: TextStyle(
                            color: Colors.red.shade900,
                            fontWeight: FontWeight.bold,
                            fontSize: 13,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          'La telemedicina no sustituye la atención de urgencias. Si presenta dolor en el pecho, dificultad respiratoria grave o pérdida del conocimiento, llame inmediatamente al 911 o acuda al centro hospitalario más cercano.',
                          style: TextStyle(
                            color: Colors.red.shade900,
                            fontSize: 11,
                            height: 1.3,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Confirmación de no emergencia
            CheckboxListTile(
              value: _noEmergency,
              onChanged: (val) => setState(() => _noEmergency = val ?? false),
              title: const Text(
                'Confirmo que mi motivo de consulta no corresponde a una emergencia vital.',
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600),
              ),
              controlAffinity: ListTileControlAffinity.leading,
              contentPadding: EdgeInsets.zero,
            ),
            const Divider(height: 30),

            // Consentimiento Informado Versionado
            const Text(
              'Consentimiento Informado (v1.2)',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            Container(
              height: 140,
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: Colors.grey.shade50,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: Colors.grey.shade300),
              ),
              child: const SingleChildScrollView(
                child: Text(
                  'Por medio del presente documento, acepto voluntariamente participar en una consulta médica a distancia mediante teleconferencia en la plataforma de telemedicina. Entiendo que:\n\n'
                  '1. El profesional de la salud evaluará mi condición y determinará si es seguro continuar el tratamiento de forma remota o si se requiere evaluación presencial.\n'
                  '2. La sesión se transmitirá por canales seguros y cifrados sin grabación permanente de video.\n'
                  '3. Se me emitirá documentación clínica oficial (receta, órdenes o indicaciones) bajo estricto criterio del médico autorizado.',
                  style: TextStyle(
                    fontSize: 11,
                    height: 1.4,
                    color: Colors.black87,
                  ),
                ),
              ),
            ),
            const SizedBox(height: 12),
            CheckboxListTile(
              value: _consentAccepted,
              onChanged: (val) =>
                  setState(() => _consentAccepted = val ?? false),
              title: const Text(
                'He leído, comprendido y acepto el consentimiento informado para telemedicina.',
                style: TextStyle(fontSize: 12),
              ),
              controlAffinity: ListTileControlAffinity.leading,
              contentPadding: EdgeInsets.zero,
            ),
            CheckboxListTile(
              value: _identityVerified,
              onChanged: (val) =>
                  setState(() => _identityVerified = val ?? false),
              title: const Text(
                'Confirmo que soy el paciente titular y los datos de mi perfil son verídicos.',
                style: TextStyle(fontSize: 12),
              ),
              controlAffinity: ListTileControlAffinity.leading,
              contentPadding: EdgeInsets.zero,
            ),
            const SizedBox(height: 28),
            CustomButton(
              text: 'Ingresar a la Sala de Espera',
              isLoading: _isSubmitting,
              onPressed: _proceedToVideo,
            ),
          ],
        ),
      ),
    );
  }
}

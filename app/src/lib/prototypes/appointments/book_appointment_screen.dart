import 'package:flutter/material.dart';
import 'package:telemedicina_app/shared/widgets/custom_button.dart';

class BookAppointmentScreen extends StatefulWidget {
  const BookAppointmentScreen({super.key});

  @override
  State<BookAppointmentScreen> createState() => _BookAppointmentScreenState();
}

class _BookAppointmentScreenState extends State<BookAppointmentScreen> {
  String _selectedSpecialty = 'Medicina Interna';
  final _reasonController = TextEditingController();
  final DateTime _selectedDate = DateTime.now().add(const Duration(days: 1));
  String _selectedSlot = '10:00 AM';
  bool _isLoading = false;

  final List<String> _specialties = [
    'Medicina General',
    'Medicina Interna',
    'Pediatría de Orientación',
    'Ginecología de Seguimiento',
    'Cardiología de Seguimiento',
    'Psicología',
    'Nutrición',
  ];

  final List<String> _slots = [
    '09:00 AM',
    '10:00 AM',
    '11:00 AM',
    '03:00 PM',
    '04:00 PM',
    '05:00 PM',
  ];

  @override
  void dispose() {
    _reasonController.dispose();
    super.dispose();
  }

  void _confirmBooking() async {
    if (_reasonController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Por favor indica el motivo de tu consulta.'),
        ),
      );
      return;
    }

    setState(() => _isLoading = true);
    await Future.delayed(const Duration(milliseconds: 800));

    if (!mounted) return;
    setState(() => _isLoading = false);

    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => AlertDialog(
        title: const Text('¡Cita Solicitada con Éxito!'),
        content: Text(
          'Tu cita de $_selectedSpecialty para el ${_selectedDate.day}/${_selectedDate.month} a las $_selectedSlot ha sido reservada.\n\nSe envió a validación con tu seguro ARS Humano.',
        ),
        actions: [
          TextButton(
            onPressed: () {
              Navigator.pop(ctx);
              Navigator.pop(context);
            },
            child: const Text('Entendido'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(title: const Text('Agendar Nueva Consulta')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text(
              'Selecciona la Especialidad',
              style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14),
              decoration: BoxDecoration(
                border: Border.all(color: Colors.grey.shade300),
                borderRadius: BorderRadius.circular(10),
              ),
              child: DropdownButtonHideUnderline(
                child: DropdownButton<String>(
                  value: _selectedSpecialty,
                  isExpanded: true,
                  items: _specialties
                      .map((s) => DropdownMenuItem(value: s, child: Text(s)))
                      .toList(),
                  onChanged: (val) => setState(() => _selectedSpecialty = val!),
                ),
              ),
            ),
            const SizedBox(height: 20),
            const Text(
              'Motivo de la Consulta',
              style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            TextField(
              controller: _reasonController,
              maxLines: 3,
              decoration: const InputDecoration(
                hintText: 'Describe brevemente tus síntomas o motivo del seguimiento médico...',
              ),
            ),
            const SizedBox(height: 20),
            const Text(
              'Horarios Disponibles',
              style: TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: _slots.map((slot) {
                final isSelected = _selectedSlot == slot;
                return ChoiceChip(
                  label: Text(slot),
                  selected: isSelected,
                  selectedColor: theme.primaryColor,
                  labelStyle: TextStyle(
                    color: isSelected ? Colors.white : Colors.black87,
                    fontWeight: isSelected
                        ? FontWeight.bold
                        : FontWeight.normal,
                  ),
                  onSelected: (val) {
                    if (val) setState(() => _selectedSlot = slot);
                  },
                );
              }).toList(),
            ),
            const SizedBox(height: 28),
            Container(
              padding: const EdgeInsets.all(14),
              decoration: BoxDecoration(
                color: Colors.teal.shade50,
                borderRadius: BorderRadius.circular(10),
                border: Border.all(color: Colors.teal.shade200),
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.shield_outlined,
                    color: Colors.teal.shade800,
                    size: 24,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'Esta consulta está cubierta por tu plan ARS Humano sujeto a copago de RD\$ 300.00 al momento de la admisión.',
                      style: TextStyle(
                        color: Colors.teal.shade900,
                        fontSize: 12,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 28),
            CustomButton(
              text: 'Confirmar y Solicitar Cita',
              isLoading: _isLoading,
              onPressed: _confirmBooking,
            ),
          ],
        ),
      ),
    );
  }
}

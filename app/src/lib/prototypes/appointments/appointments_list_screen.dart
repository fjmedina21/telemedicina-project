import 'package:flutter/material.dart';
import 'package:telemedicina_app/app/routes.dart';
import 'package:telemedicina_app/core/models/appointment.dart';
import 'package:telemedicina_app/shared/widgets/empty_state_view.dart';

class AppointmentsListScreen extends StatefulWidget {
  const AppointmentsListScreen({super.key});

  @override
  State<AppointmentsListScreen> createState() => _AppointmentsListScreenState();
}

class _AppointmentsListScreenState extends State<AppointmentsListScreen>
    with SingleTickerProviderStateMixin {
  late TabController _tabController;

  final List<Appointment> _appointments = [
    Appointment(
      id: 'apt-101',
      doctorName: 'Dra. Carmen Méndez',
      specialty: 'Medicina Interna',
      medicalCenter: 'Centro Médico Metropolitano',
      scheduledAt: DateTime.now().add(const Duration(hours: 1)),
      status: AppointmentStatus.confirmed,
      coverageStatus: CoverageStatus.approved,
      reason: 'Control de hipertensión arterial y ajuste de dosis',
    ),
    Appointment(
      id: 'apt-102',
      doctorName: 'Dr. Alejandro Morales',
      specialty: 'Cardiología de Seguimiento',
      medicalCenter: 'Centro Médico Metropolitano',
      scheduledAt: DateTime.now().add(const Duration(days: 3)),
      status: AppointmentStatus.pendingValidation,
      coverageStatus: CoverageStatus.manualReview,
      reason: 'Revisión de electrocardiograma y prueba de esfuerzo',
    ),
  ];

  @override
  void initState() {
    super.initState();
    _tabController = TabController(length: 2, vsync: this);
  }

  @override
  void dispose() {
    _tabController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      appBar: AppBar(
        title: const Text('Mis Consultas Médicas'),
        bottom: TabBar(
          controller: _tabController,
          labelColor: theme.primaryColor,
          unselectedLabelColor: Colors.grey.shade600,
          indicatorColor: theme.primaryColor,
          tabs: const [
            Tab(text: 'Próximas'),
            Tab(text: 'Historial'),
          ],
        ),
      ),
      body: TabBarView(
        controller: _tabController,
        children: [
          _buildUpcomingList(),
          const EmptyStateView(
            title: 'Sin consultas pasadas',
            message: 'Tus citas completadas y recetas aparecerán aquí.',
            icon: Icons.history_edu_outlined,
          ),
        ],
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () =>
            Navigator.pushNamed(context, AppRoutes.bookAppointment),
        icon: const Icon(Icons.add),
        label: const Text('Nueva Cita'),
      ),
    );
  }

  Widget _buildUpcomingList() {
    if (_appointments.isEmpty) {
      return const EmptyStateView(
        title: 'No tienes citas agendadas',
        message: 'Solicita una consulta médica con nuestros especialistas.',
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _appointments.length,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final apt = _appointments[index];
        return Card(
          elevation: 0,
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(14),
            side: BorderSide(color: Colors.grey.shade200),
          ),
          child: Padding(
            padding: const EdgeInsets.all(16.0),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      apt.specialty,
                      style: TextStyle(
                        color: Colors.teal.shade700,
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                      ),
                    ),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 3,
                      ),
                      decoration: BoxDecoration(
                        color: apt.coverageStatus == CoverageStatus.approved
                            ? Colors.green.shade50
                            : Colors.amber.shade50,
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        apt.coverageDisplay,
                        style: TextStyle(
                          color: apt.coverageStatus == CoverageStatus.approved
                              ? Colors.green.shade800
                              : Colors.amber.shade900,
                          fontSize: 10,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  apt.doctorName,
                  style: const TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 16,
                  ),
                ),
                const SizedBox(height: 4),
                Text(
                  apt.reason,
                  style: TextStyle(color: Colors.grey.shade600, fontSize: 13),
                ),
                const SizedBox(height: 14),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        Icon(
                          Icons.calendar_today,
                          size: 14,
                          color: Colors.grey.shade500,
                        ),
                        const SizedBox(width: 4),
                        Text(
                          '${apt.scheduledAt.day}/${apt.scheduledAt.month}/${apt.scheduledAt.year}',
                          style: TextStyle(
                            fontSize: 12,
                            color: Colors.grey.shade700,
                          ),
                        ),
                      ],
                    ),
                    ElevatedButton(
                      onPressed: () {
                        Navigator.pushNamed(
                          context,
                          AppRoutes.checkIn,
                          arguments: apt,
                        );
                      },
                      style: ElevatedButton.styleFrom(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 14,
                          vertical: 6,
                        ),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(8),
                        ),
                      ),
                      child: const Text(
                        'Admisión Virtual',
                        style: TextStyle(fontSize: 12),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}

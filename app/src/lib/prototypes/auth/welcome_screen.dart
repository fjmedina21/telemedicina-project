import 'package:flutter/material.dart';
import 'package:telemedicina_app/app/routes.dart';
import 'package:telemedicina_app/shared/widgets/custom_button.dart';

class WelcomeScreen extends StatelessWidget {
  const WelcomeScreen({super.key});

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Scaffold(
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 28.0, vertical: 24.0),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const SizedBox(height: 20),
              Column(
                children: [
                  Container(
                    width: 90,
                    height: 90,
                    decoration: BoxDecoration(
                      color: theme.primaryColor.withValues(alpha: 0.12),
                      shape: BoxShape.circle,
                    ),
                    child: Icon(
                      Icons.medical_services_rounded,
                      size: 48,
                      color: theme.primaryColor,
                    ),
                  ),
                  const SizedBox(height: 24),
                  const Text(
                    'Telemedicina Integral',
                    style: TextStyle(
                      fontSize: 26,
                      fontWeight: FontWeight.bold,
                      letterSpacing: -0.5,
                    ),
                    textAlign: TextAlign.center,
                  ),
                  const SizedBox(height: 12),
                  Text(
                    'Atención médica remota, segura y trazable para ti y tu familia en República Dominicana.',
                    style: TextStyle(
                      fontSize: 15,
                      color: Colors.grey.shade600,
                      height: 1.4,
                    ),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  CustomButton(
                    text: 'Iniciar Sesión',
                    onPressed: () =>
                        Navigator.pushNamed(context, AppRoutes.login),
                  ),
                  const SizedBox(height: 12),
                  CustomButton(
                    text: 'Crear una Cuenta',
                    isSecondary: true,
                    onPressed: () =>
                        Navigator.pushNamed(context, AppRoutes.register),
                  ),
                  const SizedBox(height: 16),
                  Text(
                    'Al continuar, aceptas nuestros términos de servicio y políticas de privacidad clínica.',
                    style: TextStyle(fontSize: 11, color: Colors.grey.shade500),
                    textAlign: TextAlign.center,
                  ),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}

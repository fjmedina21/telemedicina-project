import 'package:flutter/material.dart';

import '../data/auth_repository.dart';
import '../../../shared/presentation/actions.dart';
import '../../../shared/widgets/labeled_text_field.dart';

class AuthForm extends StatefulWidget {
  final AuthRepository auth;
  final AsyncActionRunner run;
  final Future<void> Function(String, String) onLogin;
  final ValueChanged<String> onMessage;
  final bool busy;
  const AuthForm({
    super.key,
    required this.auth,
    required this.run,
    required this.onLogin,
    required this.onMessage,
    required this.busy,
  });
  @override
  State<AuthForm> createState() => _AuthFormState();
}

class _AuthFormState extends State<AuthForm> {
  final email = TextEditingController(),
      password = TextEditingController(),
      token = TextEditingController();
  String mode = 'login';
  @override
  void dispose() {
    email.dispose();
    password.dispose();
    token.dispose();
    super.dispose();
  }

  Future<void> submit() async {
    if (mode == 'login') {
      await widget.onLogin(email.text.trim(), password.text);
    } else if (mode == 'register') {
      await widget.auth.register(email.text.trim(), password.text);
      if (mounted) {
        setState(() => mode = 'verify');
        widget.onMessage('Revisa tu correo para verificar la cuenta.');
      }
    } else if (mode == 'verify') {
      final code = token.text.trim().toUpperCase();
      if (!RegExp(r'^[A-Z0-9]{6}$').hasMatch(code)) {
        widget.onMessage(
          'Introduce el código de 6 letras o números recibido por correo.',
        );
        return;
      }
      await widget.auth.verify(email.text.trim(), code);
      token.clear();
      if (mounted) {
        setState(() => mode = 'login');
        widget.onMessage('Cuenta verificada. Inicie sesión.');
      }
    } else if (mode == 'forgot') {
      await widget.auth.forgot(email.text.trim());
      if (mounted) {
        setState(() => mode = 'reset');
        widget.onMessage('Si corresponde, revisa tu correo.');
      }
    } else {
      await widget.auth.reset(token.text.trim(), password.text);
      token.clear();
      password.clear();
      if (mounted) {
        setState(() => mode = 'login');
        widget.onMessage('Contraseña actualizada.');
      }
    }
  }

  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(
        'TU ESPACIO DE SALUD',
        style: TextStyle(
          fontSize: 10,
          letterSpacing: 1.8,
          color: Theme.of(context).colorScheme.primary,
          fontWeight: FontWeight.w700,
        ),
      ),
      const SizedBox(height: 16),
      Text(
        {
          'login': 'Qué bueno verte',
          'register': 'Crea tu cuenta',
          'verify': 'Verifica tu correo',
          'forgot': 'Recupera tu acceso',
          'reset': 'Una nueva contraseña',
        }[mode]!,
        style: Theme.of(context).textTheme.headlineMedium,
      ),
      const SizedBox(height: 12),
      Text(
        {
          'login': 'Tus citas y avisos, en un mismo lugar.',
          'register':
              'Regístrate para completar tu perfil y solicitar una cita.',
          'verify': 'Introduce las 6 letras o números que enviamos a tu correo. El código dura 15 minutos.',
          'forgot': 'Te enviaremos las instrucciones para recuperar tu cuenta.',
          'reset':
              'Introduce el código recibido y elige una contraseña segura.',
        }[mode]!,
        style: TextStyle(color: Theme.of(context).colorScheme.onSurfaceVariant),
      ),
      const SizedBox(height: 24),
      if (['login', 'register', 'forgot', 'verify'].contains(mode))
        LabeledTextField(
          controller: email,
          label: 'Correo electrónico',
          keyboardType: TextInputType.emailAddress,
          autofillHints: const [AutofillHints.username],
          enabled: !widget.busy,
        ),
      if (['login', 'register', 'reset'].contains(mode))
        LabeledTextField(
          controller: password,
          label: 'Contraseña (mínimo 12 caracteres)',
          secret: true,
          autofillHints: [
            mode == 'login'
                ? AutofillHints.password
                : AutofillHints.newPassword,
          ],
          enabled: !widget.busy,
        ),
      if (['verify', 'reset'].contains(mode))
        LabeledTextField(
          controller: token,
          maxLength: mode == 'verify' ? 6 : null,
          capitalization: mode == 'verify'
              ? TextCapitalization.characters
              : TextCapitalization.none,
          autofillHints: const [AutofillHints.oneTimeCode],
          label: mode == 'verify'
              ? 'Código de verificación (6 letras o números)'
              : 'Código recibido por correo',
          enabled: !widget.busy,
        ),
      if (mode == 'verify')
        TextButton(
          onPressed: widget.busy
              ? null
              : () => widget.run(() async {
                  await widget.auth.resend(email.text.trim());
                  widget.onMessage('Si corresponde, revisa tu correo.');
                }),
          child: const Text('Reenviar código'),
        ),
      const SizedBox(height: 12),
      FilledButton(
        onPressed: widget.busy ? null : () => widget.run(submit),
        child: Text(
          {
            'login': 'Entrar',
            'register': 'Crear cuenta',
            'verify': 'Verificar cuenta',
            'forgot': 'Solicitar recuperación',
            'reset': 'Cambiar contraseña',
          }[mode]!,
        ),
      ),
      const SizedBox(height: 18),
      Wrap(
        alignment: WrapAlignment.center,
        spacing: 4,
        children: [
          for (final entry in {
            'login': 'Iniciar sesión',
            'register': 'Registrarme',
            'verify': 'Verificar',
            'forgot': 'Recuperar acceso',
          }.entries.where((entry) => entry.key != mode))
            TextButton(
              onPressed: widget.busy
                  ? null
                  : () {
                      setState(() => mode = entry.key);
                      widget.onMessage('');
                    },
              child: Text(entry.value),
            ),
        ],
      ),
    ],
  );
}

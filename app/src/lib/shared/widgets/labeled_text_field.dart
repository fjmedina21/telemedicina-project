import 'package:flutter/material.dart';

class LabeledTextField extends StatefulWidget {
  final TextEditingController controller;
  final String label;
  final bool enabled, secret;
  final TextInputType? keyboardType;
  final Iterable<String>? autofillHints;
  final int? maxLength;
  final TextCapitalization capitalization;
  const LabeledTextField({
    super.key,
    required this.controller,
    required this.label,
    this.enabled = true,
    this.secret = false,
    this.keyboardType,
    this.autofillHints,
    this.maxLength,
    this.capitalization = TextCapitalization.none,
  });
  @override
  State<LabeledTextField> createState() => _LabeledTextFieldState();
}

class _LabeledTextFieldState extends State<LabeledTextField> {
  bool reveal = false;
  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 8),
    child: TextField(
      controller: widget.controller,
      enabled: widget.enabled,
      obscureText: widget.secret && !reveal,
      keyboardType: widget.keyboardType,
      autofillHints: widget.autofillHints,
      maxLength: widget.maxLength,
      textCapitalization: widget.capitalization,
      decoration: InputDecoration(
        labelText: widget.label,
        suffixIcon: widget.secret
            ? IconButton(
                tooltip: reveal ? 'Ocultar contraseña' : 'Mostrar contraseña',
                onPressed: widget.enabled
                    ? () => setState(() => reveal = !reveal)
                    : null,
                icon: Icon(
                  reveal
                      ? Icons.visibility_off_outlined
                      : Icons.visibility_outlined,
                  size: 20,
                ),
              )
            : null,
      ),
    ),
  );
}

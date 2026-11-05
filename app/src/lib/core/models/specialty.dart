class Specialty {
  final String id;
  final String name;
  final String description;
  final String iconCode;
  final bool allowsVirtual;
  final double standardFeeDOP;

  const Specialty({
    required this.id,
    required this.name,
    required this.description,
    this.iconCode = 'medical_services',
    this.allowsVirtual = true,
    this.standardFeeDOP = 2500.0,
  });
}

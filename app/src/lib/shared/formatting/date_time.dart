String localTime(String value) {
  final date = DateTime.parse(value).toUtc().subtract(const Duration(hours: 4));
  String pad(int number) => number.toString().padLeft(2, '0');
  return '${pad(date.day)}/${pad(date.month)}/${date.year} ${pad(date.hour)}:${pad(date.minute)} (Santo Domingo)';
}

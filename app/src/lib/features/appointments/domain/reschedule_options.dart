import '../../../core/models/json_map.dart';

class RescheduleOptions {
  final JsonMap appointment;
  final List<JsonMap> slots;
  const RescheduleOptions(this.appointment, this.slots);
}

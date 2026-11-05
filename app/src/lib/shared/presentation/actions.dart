typedef AsyncActionRunner = Future<bool> Function(
  Future<void> Function() action,
);

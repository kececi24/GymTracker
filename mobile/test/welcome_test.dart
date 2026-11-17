import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:gymtracker/main.dart';

void main() {
  testWidgets('shows the welcome screen without external services', (
    tester,
  ) async {
    await tester.pumpWidget(const ProviderScope(child: GymTrackerApp()));

    expect(find.text('GymTracker'), findsOneWidget);
    expect(find.text('Plan, log, and review your training.'), findsOneWidget);
    expect(find.text('API connection is unconfigured.'), findsOneWidget);
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/test_harness.dart';

void main() {
  Future<void> signIn(WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp());
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const ValueKey('auth-email')),
      'ahmet@example.com',
    );
    await tester.enterText(
      find.byKey(const ValueKey('auth-password')),
      'secret123',
    );
    await tester.tap(find.byKey(const ValueKey('auth-submit')));
    await tester.pumpAndSettle();
  }

  testWidgets('bottom navigation switches between primary pages', (
    tester,
  ) async {
    await signIn(tester);

    await tester.tap(find.text('Workouts'));
    await tester.pumpAndSettle();
    expect(find.text('Workouts are on the way'), findsOneWidget);

    await tester.tap(find.text('History'));
    await tester.pumpAndSettle();
    expect(find.text('History will appear here'), findsOneWidget);

    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();
    expect(find.text('ahmet@example.com'), findsOneWidget);

    await tester.tap(find.text('Home'));
    await tester.pumpAndSettle();
    expect(find.text('Hi, Ahmet'), findsOneWidget);
  });

  testWidgets('profile sign out returns to the authentication screen', (
    tester,
  ) async {
    await signIn(tester);

    await tester.tap(find.text('Profile'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('profile-sign-out')));
    await tester.pumpAndSettle();

    expect(find.text('Sign out?'), findsOneWidget);

    await tester.tap(find.widgetWithText(FilledButton, 'Sign out'));
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('auth-screen')), findsOneWidget);
    expect(find.text('Welcome back'), findsOneWidget);
  });
}

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'support/test_harness.dart';

void main() {
  Future<void> pumpApp(WidgetTester tester) async {
    await tester.pumpWidget(buildTestApp());
    await tester.pumpAndSettle();
  }

  testWidgets('starts on the authentication screen', (tester) async {
    await pumpApp(tester);

    expect(find.byKey(const ValueKey('auth-screen')), findsOneWidget);
    expect(find.text('Welcome back'), findsOneWidget);
    expect(find.text('Plan, log, and review your training.'), findsOneWidget);
  });

  testWidgets('validates the sign-in form', (tester) async {
    await pumpApp(tester);

    await tester.tap(find.byKey(const ValueKey('auth-submit')));
    await tester.pump();

    expect(find.text('Enter your email address.'), findsOneWidget);
    expect(find.text('Enter your password.'), findsOneWidget);
  });

  testWidgets('switches to registration mode', (tester) async {
    await pumpApp(tester);

    final toggle = find.byKey(const ValueKey('auth-toggle-mode'));
    await tester.ensureVisible(toggle);
    await tester.pumpAndSettle();
    await tester.tap(toggle);
    await tester.pumpAndSettle();

    expect(find.text('Create your account'), findsOneWidget);
    expect(find.byKey(const ValueKey('auth-display-name')), findsOneWidget);
  });

  testWidgets('signs in and opens the navigation shell', (tester) async {
    await pumpApp(tester);

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

    expect(find.byKey(const ValueKey('app-shell')), findsOneWidget);
    expect(find.text('Hi, Ahmet'), findsOneWidget);
    expect(find.text('Sign in'), findsNothing);
  });

  testWidgets('signs in with Google', (tester) async {
    await pumpApp(tester);

    final googleButton = find.byKey(const ValueKey('auth-google'));
    await tester.ensureVisible(googleButton);
    await tester.pumpAndSettle();
    await tester.tap(googleButton);
    await tester.pumpAndSettle();

    expect(find.byKey(const ValueKey('app-shell')), findsOneWidget);
    expect(find.text('Hi, Google'), findsOneWidget);
  });
}

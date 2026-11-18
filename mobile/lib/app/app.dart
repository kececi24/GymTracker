import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../features/auth/auth_controller.dart';
import '../features/auth/auth_screen.dart';
import '../features/shell/app_shell.dart';
import 'startup_screen.dart';
import 'theme.dart';

class GymTrackerApp extends ConsumerWidget {
  const GymTrackerApp({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final status = ref.watch(
      authControllerProvider.select((state) => state.status),
    );

    final screen = switch (status) {
      AuthStatus.unknown => const StartupScreen(),
      AuthStatus.signedIn => const AppShell(),
      AuthStatus.signedOut || AuthStatus.authenticating => const AuthScreen(),
    };

    return MaterialApp(
      title: 'GymTracker',
      debugShowCheckedModeBanner: false,
      theme: buildAppTheme(),
      home: AnimatedSwitcher(
        duration: const Duration(milliseconds: 250),
        child: screen,
      ),
    );
  }
}

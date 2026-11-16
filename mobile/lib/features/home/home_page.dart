import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import '../../core/api/api_client.dart';
import '../../core/widgets/coming_soon.dart';
import '../auth/auth_controller.dart';

class HomePage extends ConsumerWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    final theme = Theme.of(context);
    final colorScheme = theme.colorScheme;
    final authState = ref.watch(authControllerProvider);
    final firstName = _firstName(authState.displayName);

    return Scaffold(
      appBar: AppBar(
        title: const Text('GymTracker'),
        actions: [
          IconButton(
            tooltip: 'Notifications',
            onPressed: () => showComingSoonSnackBar(context, 'Notifications'),
            icon: const Icon(Icons.notifications_none_outlined),
          ),
        ],
      ),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(20, 4, 20, 28),
        children: [
          Text(
            'Hi, $firstName',
            style: theme.textTheme.headlineSmall?.copyWith(
              fontWeight: FontWeight.w800,
            ),
          ),
          const SizedBox(height: 4),
          Text(
            'Ready for your next session?',
            style: theme.textTheme.bodyLarge?.copyWith(
              color: colorScheme.onSurfaceVariant,
            ),
          ),
          const SizedBox(height: 24),
          _TodayCard(colorScheme: colorScheme),
          const SizedBox(height: 16),
          const _BackendStatusCard(),
        ],
      ),
    );
  }

  static String _firstName(String? displayName) {
    final name = (displayName ?? '').trim();
    if (name.isEmpty) {
      return 'Athlete';
    }
    return name.split(RegExp(r'\s+')).first;
  }
}

class _TodayCard extends StatelessWidget {
  const _TodayCard({required this.colorScheme});

  final ColorScheme colorScheme;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(
                  Icons.today_outlined,
                  size: 20,
                  color: colorScheme.primary,
                ),
                const SizedBox(width: 8),
                Text(
                  'Today',
                  style: theme.textTheme.titleSmall?.copyWith(
                    color: colorScheme.primary,
                    fontWeight: FontWeight.w700,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 14),
            Text(
              'No workout planned yet',
              style: theme.textTheme.titleLarge?.copyWith(
                fontWeight: FontWeight.w700,
              ),
            ),
            const SizedBox(height: 6),
            Text(
              'Your daily plan, reminders, and progress summary will appear '
              'here once programs and scheduling are wired up.',
              style: theme.textTheme.bodyMedium?.copyWith(
                color: colorScheme.onSurfaceVariant,
                height: 1.4,
              ),
            ),
            const SizedBox(height: 16),
            FilledButton.tonalIcon(
              onPressed: () =>
                  showComingSoonSnackBar(context, 'Workout logging'),
              icon: const Icon(Icons.play_arrow_rounded),
              label: const Text('Start a workout'),
            ),
          ],
        ),
      ),
    );
  }
}

class _BackendStatusCard extends ConsumerWidget {
  const _BackendStatusCard();

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');
    final colorScheme = Theme.of(context).colorScheme;

    if (apiBaseUrl.isEmpty) {
      return Card(
        child: ListTile(
          leading: Icon(Icons.cloud_off_outlined, color: colorScheme.outline),
          title: const Text('Backend'),
          subtitle: const Text('API connection is unconfigured.'),
        ),
      );
    }

    return Card(
      child: ref
          .watch(apiHealthProvider)
          .when(
            loading: () => const ListTile(
              leading: SizedBox(
                width: 24,
                height: 24,
                child: CircularProgressIndicator(strokeWidth: 2.5),
              ),
              title: Text('Backend'),
              subtitle: Text('Connecting to the API…'),
            ),
            data: (isHealthy) => ListTile(
              leading: Icon(
                isHealthy
                    ? Icons.cloud_done_outlined
                    : Icons.cloud_off_outlined,
                color: isHealthy ? colorScheme.primary : colorScheme.error,
              ),
              title: const Text('Backend'),
              subtitle: Text(
                isHealthy ? 'API connected.' : 'API is unavailable.',
              ),
              trailing: IconButton(
                tooltip: 'Check connection',
                onPressed: () => ref.invalidate(apiHealthProvider),
                icon: const Icon(Icons.refresh),
              ),
            ),
            error: (error, stackTrace) => ListTile(
              leading: Icon(Icons.cloud_off_outlined, color: colorScheme.error),
              title: const Text('Backend'),
              subtitle: const Text('API is unavailable.'),
              trailing: IconButton(
                tooltip: 'Check connection',
                onPressed: () => ref.invalidate(apiHealthProvider),
                icon: const Icon(Icons.refresh),
              ),
            ),
          ),
    );
  }
}

import 'package:dio/dio.dart';
import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

void main() => runApp(const ProviderScope(child: GymTrackerApp()));

final apiClientProvider = Provider<Dio>((ref) {
  final client = Dio(
    BaseOptions(
      baseUrl: const String.fromEnvironment('API_BASE_URL'),
      connectTimeout: const Duration(seconds: 5),
      receiveTimeout: const Duration(seconds: 5),
    ),
  );
  ref.onDispose(client.close);
  return client;
});

final apiHealthProvider = FutureProvider.autoDispose<bool>((ref) async {
  final client = ref.watch(apiClientProvider);
  final response = await client.get<Map<String, dynamic>>('/health');
  return response.data?['status'] == 'ok';
});

class GymTrackerApp extends StatelessWidget {
  const GymTrackerApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'GymTracker',
      theme: ThemeData(colorSchemeSeed: Colors.teal),
      home: const WelcomeScreen(),
    );
  }
}

class WelcomeScreen extends ConsumerWidget {
  const WelcomeScreen({super.key});

  @override
  Widget build(BuildContext context, WidgetRef ref) {
    const apiBaseUrl = String.fromEnvironment('API_BASE_URL');
    final status = apiBaseUrl.isEmpty
        ? 'API connection is unconfigured.'
        : ref
              .watch(apiHealthProvider)
              .when(
                data: (healthy) =>
                    healthy ? 'API connected.' : 'API is unavailable.',
                error: (error, stackTrace) => 'API is unavailable.',
                loading: () => 'Connecting to the API…',
              );

    return Scaffold(
      appBar: AppBar(title: const Text('GymTracker')),
      body: ListView(
        padding: const EdgeInsets.all(24),
        children: [
          Text(
            'Plan, log, and review your training.',
            style: Theme.of(context).textTheme.headlineSmall,
          ),
          const SizedBox(height: 16),
          Text(status),
          if (apiBaseUrl.isNotEmpty)
            OutlinedButton(
              onPressed: () => ref.invalidate(apiHealthProvider),
              child: const Text('Check connection'),
            ),
        ],
      ),
    );
  }
}

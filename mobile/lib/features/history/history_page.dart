import 'package:flutter/material.dart';

import '../../core/widgets/coming_soon.dart';

class HistoryPage extends StatelessWidget {
  const HistoryPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('History')),
      body: const ComingSoonView(
        icon: Icons.history,
        title: 'History will appear here',
        message:
            'Completed workouts, personal records, and progress charts will '
            'be available in a later iteration.',
      ),
    );
  }
}

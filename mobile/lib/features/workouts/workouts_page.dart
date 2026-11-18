import 'package:flutter/material.dart';

import '../../core/widgets/coming_soon.dart';

class WorkoutsPage extends StatelessWidget {
  const WorkoutsPage({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Workouts')),
      body: const ComingSoonView(
        icon: Icons.fitness_center_rounded,
        title: 'Workouts are on the way',
        message:
            'Log exercises, sets, reps, and load here. Manual workout entry '
            'and AI-assisted logging will land in a later iteration.',
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: () => showComingSoonSnackBar(context, 'Workout logging'),
        icon: const Icon(Icons.add),
        label: const Text('Log workout'),
      ),
    );
  }
}

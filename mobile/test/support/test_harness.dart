import 'package:flutter/material.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:gymtracker/app/app.dart';
import 'package:gymtracker/features/auth/auth_repository.dart';

class FakeAuthRepository implements AuthRepository {
  AuthUser? _currentUser;

  @override
  AuthUser? get currentUser => _currentUser;

  @override
  Stream<AuthUser?> authStateChanges() => Stream<AuthUser?>.value(_currentUser);

  @override
  Future<AuthUser> signInWithEmail({
    required String email,
    required String password,
  }) async {
    return _setUser(
      AuthUser(uid: 'test-uid', email: email.trim(), displayName: 'Ahmet'),
    );
  }

  @override
  Future<AuthUser> registerWithEmail({
    required String displayName,
    required String email,
    required String password,
  }) async {
    return _setUser(
      AuthUser(
        uid: 'test-uid',
        email: email.trim(),
        displayName: displayName.trim(),
      ),
    );
  }

  @override
  Future<AuthUser> signInWithGoogle() async {
    return _setUser(
      const AuthUser(
        uid: 'google-uid',
        email: 'google@example.com',
        displayName: 'Google Athlete',
      ),
    );
  }

  @override
  Future<void> signOut() async {
    _currentUser = null;
  }

  AuthUser _setUser(AuthUser user) {
    _currentUser = user;
    return user;
  }
}

Widget buildTestApp({AuthRepository? repository}) {
  return ProviderScope(
    overrides: [
      authRepositoryProvider.overrideWithValue(
        repository ?? FakeAuthRepository(),
      ),
    ],
    child: const GymTrackerApp(),
  );
}

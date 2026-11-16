import 'package:flutter/foundation.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';

import 'auth_repository.dart';

enum AuthStatus { unknown, signedOut, authenticating, signedIn }

@immutable
class AuthState {
  const AuthState({
    this.status = AuthStatus.signedOut,
    this.uid,
    this.email,
    this.displayName,
    this.errorMessage,
  });

  final AuthStatus status;
  final String? uid;
  final String? email;
  final String? displayName;
  final String? errorMessage;

  bool get isSignedIn => status == AuthStatus.signedIn;
  bool get isBusy => status == AuthStatus.authenticating;
  bool get isInitializing => status == AuthStatus.unknown;
}

class AuthController extends Notifier<AuthState> {
  @override
  AuthState build() {
    final repository = ref.watch(authRepositoryProvider);
    final subscription = repository.authStateChanges().listen(
      (user) => state = _stateFor(user),
      onError: (Object error, StackTrace stackTrace) {
        state = const AuthState(
          errorMessage: 'Could not restore your session. Please sign in again.',
        );
      },
    );

    ref.onDispose(subscription.cancel);
    return const AuthState(status: AuthStatus.unknown);
  }

  Future<bool> signIn({required String email, required String password}) {
    return _run(
      () => ref
          .read(authRepositoryProvider)
          .signInWithEmail(email: email, password: password),
    );
  }

  Future<bool> register({
    required String displayName,
    required String email,
    required String password,
  }) {
    return _run(
      () => ref
          .read(authRepositoryProvider)
          .registerWithEmail(
            displayName: displayName,
            email: email,
            password: password,
          ),
    );
  }

  Future<bool> signInWithGoogle() {
    return _run(() => ref.read(authRepositoryProvider).signInWithGoogle());
  }

  Future<bool> signOut() async {
    final previous = state;
    try {
      await ref.read(authRepositoryProvider).signOut();
      state = const AuthState();
      return true;
    } catch (_) {
      state = AuthState(
        status: previous.status,
        uid: previous.uid,
        email: previous.email,
        displayName: previous.displayName,
        errorMessage: 'Sign-out failed. Please try again.',
      );
      return false;
    }
  }

  Future<bool> _run(Future<AuthUser> Function() action) async {
    state = const AuthState(status: AuthStatus.authenticating);

    try {
      final user = await action();
      state = _stateFor(user);
      return true;
    } on AuthCancelled {
      state = const AuthState();
      return false;
    } on AuthFailure catch (error) {
      state = AuthState(errorMessage: error.message);
      return false;
    } catch (_) {
      state = const AuthState(
        errorMessage: 'Something went wrong. Please try again.',
      );
      return false;
    }
  }

  static AuthState _stateFor(AuthUser? user) {
    if (user == null) {
      return const AuthState();
    }

    return AuthState(
      status: AuthStatus.signedIn,
      uid: user.uid,
      email: user.email,
      displayName: user.displayName,
    );
  }
}

final authControllerProvider = NotifierProvider<AuthController, AuthState>(
  AuthController.new,
);

import 'package:firebase_auth/firebase_auth.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter/services.dart';
import 'package:flutter_riverpod/flutter_riverpod.dart';
import 'package:google_sign_in/google_sign_in.dart';

@immutable
class AuthUser {
  const AuthUser({required this.uid, this.email, this.displayName});

  final String uid;
  final String? email;
  final String? displayName;
}

/// A user-facing authentication error.
class AuthFailure implements Exception {
  const AuthFailure(this.message);

  final String message;

  @override
  String toString() => message;
}

/// Thrown when the user dismisses an external sign-in prompt.
class AuthCancelled implements Exception {
  const AuthCancelled();
}

abstract interface class AuthRepository {
  AuthUser? get currentUser;

  Stream<AuthUser?> authStateChanges();

  Future<AuthUser> signInWithEmail({
    required String email,
    required String password,
  });

  Future<AuthUser> registerWithEmail({
    required String displayName,
    required String email,
    required String password,
  });

  Future<AuthUser> signInWithGoogle();

  Future<void> signOut();
}

final authRepositoryProvider = Provider<AuthRepository>(
  (ref) => FirebaseAuthRepository(),
);

class FirebaseAuthRepository implements AuthRepository {
  FirebaseAuth get _auth => FirebaseAuth.instance;

  Future<void>? _googleInitialization;

  @override
  AuthUser? get currentUser => _toAuthUser(_auth.currentUser);

  @override
  Stream<AuthUser?> authStateChanges() =>
      _auth.authStateChanges().map(_toAuthUser);

  @override
  Future<AuthUser> signInWithEmail({
    required String email,
    required String password,
  }) async {
    try {
      final credential = await _auth.signInWithEmailAndPassword(
        email: email.trim(),
        password: password,
      );
      return _requireUser(credential.user);
    } on FirebaseAuthException catch (error) {
      throw AuthFailure(_messageForFirebaseError(error));
    }
  }

  @override
  Future<AuthUser> registerWithEmail({
    required String displayName,
    required String email,
    required String password,
  }) async {
    try {
      final credential = await _auth.createUserWithEmailAndPassword(
        email: email.trim(),
        password: password,
      );
      final user = _requireFirebaseUser(credential.user);
      final name = displayName.trim();

      if (name.isNotEmpty) {
        try {
          await user.updateDisplayName(name);
        } on FirebaseAuthException {
          // The account is usable even if the profile name could not be saved.
        }
      }

      return AuthUser(
        uid: user.uid,
        email: user.email,
        displayName: name.isEmpty ? _displayNameFromEmail(user.email) : name,
      );
    } on FirebaseAuthException catch (error) {
      throw AuthFailure(_messageForFirebaseError(error));
    }
  }

  @override
  Future<AuthUser> signInWithGoogle() async {
    final googleSignIn = GoogleSignIn.instance;

    if (!googleSignIn.supportsAuthenticate()) {
      throw const AuthFailure(
        'Google sign-in is only available in the Android and iOS apps.',
      );
    }

    try {
      await (_googleInitialization ??= googleSignIn.initialize());

      final googleUser = await googleSignIn.authenticate();
      final idToken = googleUser.authentication.idToken;

      if (idToken == null || idToken.isEmpty) {
        throw const AuthFailure(
          'Google did not return an ID token. Check the SHA fingerprints '
          'registered for the Android app in Firebase.',
        );
      }

      final credential = GoogleAuthProvider.credential(idToken: idToken);
      final userCredential = await _auth.signInWithCredential(credential);
      return _requireUser(userCredential.user);
    } on GoogleSignInException catch (error) {
      if (error.code == GoogleSignInExceptionCode.canceled) {
        throw const AuthCancelled();
      }
      throw AuthFailure(_messageForGoogleError(error));
    } on FirebaseAuthException catch (error) {
      throw AuthFailure(_messageForFirebaseError(error));
    } on PlatformException catch (error) {
      throw AuthFailure(
        error.message ?? 'Google sign-in failed. Please try again.',
      );
    }
  }

  @override
  Future<void> signOut() async {
    try {
      await (_googleInitialization ??= GoogleSignIn.instance.initialize());
      await GoogleSignIn.instance.signOut();
    } catch (_) {
      // Clearing the Google session is best-effort. Always end the Firebase
      // session so the user is signed out locally.
    }
    await _auth.signOut();
  }

  User _requireFirebaseUser(User? user) {
    if (user == null) {
      throw const AuthFailure('Sign-in failed. Please try again.');
    }
    return user;
  }

  AuthUser _requireUser(User? user) {
    final authUser = _toAuthUser(user);
    if (authUser == null) {
      throw const AuthFailure('Sign-in failed. Please try again.');
    }
    return authUser;
  }

  AuthUser? _toAuthUser(User? user) {
    if (user == null) {
      return null;
    }

    final name = user.displayName?.trim();
    return AuthUser(
      uid: user.uid,
      email: user.email,
      displayName: name == null || name.isEmpty
          ? _displayNameFromEmail(user.email)
          : name,
    );
  }

  static String _displayNameFromEmail(String? email) {
    final localPart = (email ?? '')
        .trim()
        .split('@')
        .first
        .replaceAll(RegExp(r'[._-]+'), ' ')
        .trim();

    if (localPart.isEmpty) {
      return 'Athlete';
    }

    return localPart
        .split(' ')
        .where((word) => word.isNotEmpty)
        .map((word) => '${word[0].toUpperCase()}${word.substring(1)}')
        .join(' ');
  }

  static String _messageForFirebaseError(FirebaseAuthException error) {
    return switch (error.code) {
      'invalid-email' => 'That email address looks invalid.',
      'user-disabled' => 'This account has been disabled.',
      'user-not-found' ||
      'wrong-password' ||
      'invalid-credential' => 'Incorrect email or password.',
      'email-already-in-use' => 'An account already exists for this email.',
      'weak-password' => 'Choose a password with at least 6 characters.',
      'operation-not-allowed' =>
        'This sign-in method is not enabled in Firebase.',
      'network-request-failed' => 'Check your connection and try again.',
      'too-many-requests' => 'Too many attempts. Please try again later.',
      _ => 'Sign-in failed. Please try again.',
    };
  }

  static String _messageForGoogleError(GoogleSignInException error) {
    if (error.code == GoogleSignInExceptionCode.clientConfigurationError ||
        error.code == GoogleSignInExceptionCode.providerConfigurationError) {
      return 'Google sign-in is not configured correctly. Check the Firebase '
          'Google provider and the Android SHA fingerprints.';
    }
    return error.description ?? 'Google sign-in failed. Please try again.';
  }
}

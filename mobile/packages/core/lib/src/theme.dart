import 'package:flutter/material.dart';

class AppTheme {
  static const brand = Color(0xFF0F7B6C);

  static ThemeData light() => ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: brand),
        useMaterial3: true,
      );

  static ThemeData dark() => ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: brand, brightness: Brightness.dark),
        useMaterial3: true,
      );
}

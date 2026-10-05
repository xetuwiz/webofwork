import 'dart:async';
import 'dart:io';

import 'package:connectivity_plus/connectivity_plus.dart';
import 'package:flutter/foundation.dart' show kDebugMode;
import 'package:flutter/material.dart';
import 'package:lottie/lottie.dart';
import 'package:webofwork/helper/extension/context_extension.dart';
import 'package:webofwork/helper/extension/int_extension.dart';
import 'package:webofwork/helper/local_keys.g.dart';
import 'package:webofwork/utils/components/custom_button.dart';

class NetworkConnectivity {
  NetworkConnectivity._();
  static final _instance = NetworkConnectivity._();
  static NetworkConnectivity get instance => _instance;
  final _networkConnectivity = Connectivity();
  final _controller = StreamController.broadcast();
  Stream get myStream => _controller.stream;
  void initialise() async {
    final results = await _networkConnectivity.checkConnectivity();
    _checkStatus(results);
    _networkConnectivity.onConnectivityChanged.listen((results) {
      print(results);
      _checkStatus(results);
    });
  }

  Future<bool> currentStatus() async {
    bool isOnline = false;
    try {
      final result = await InternetAddress.lookup('google.com');
      isOnline = result.isNotEmpty && result[0].rawAddress.isNotEmpty;
    } on SocketException catch (_) {
      isOnline = false;
    }
    return isOnline;
  }

  void _checkStatus(List<ConnectivityResult> results) async {
    bool isOnline = false;
    try {
      final lookup = await InternetAddress.lookup('google.com');
      isOnline = lookup.isNotEmpty && lookup[0].rawAddress.isNotEmpty;
    } on SocketException catch (_) {
      isOnline = false;
    }
    // Map the v6 list back onto the single result the listeners expect.
    ConnectivityResult result = ConnectivityResult.none;
    if (results.contains(ConnectivityResult.wifi)) {
      result = ConnectivityResult.wifi;
    } else if (results.contains(ConnectivityResult.mobile)) {
      result = ConnectivityResult.mobile;
    } else if (results.contains(ConnectivityResult.ethernet)) {
      result = ConnectivityResult.ethernet;
    }
    _controller.sink.add({result: isOnline});
  }

  void disposeStream() => _controller.close();

  listenToConnectionChange(BuildContext context) {
    // The old `Map source = {ConnectivityResult.none: false};` here was dead:
    // the `myStream.listen` callback below declares its own `source` parameter,
    // so the outer one was shadowed on the very first event and the analyzer was
    // right. The only real reason it existed was to feed `source = source;`,
    // which assigns a variable to itself and does nothing.
    String string = '';
    initialise();
    bool isOffline = false;
    myStream.listen((source) {
      if (kDebugMode) debugPrint('connectivity: $source');

      switch (source.keys.toList()[0]) {
        case ConnectivityResult.mobile:
          string = source.values.toList()[0] ? 'Mobile: Online' : 'Mobile: Offline';
          break;
        case ConnectivityResult.wifi:
          string = source.values.toList()[0] ? 'WiFi: Online' : 'WiFi: Offline';
          break;
        case ConnectivityResult.none:
        default:
          string = 'Offline';
      }
      if (string.contains('Online') && isOffline) {
        isOffline = false;
        Navigator.pop(context);
      }
      if (string.contains('Offline') && !isOffline) {
        isOffline = true;
        showDialog(
          context: context,
          builder: (context) => AlertDialog(
            content: Container(
              height: 400,
              decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(16),
                  color: context.dProvider.whiteColor),
              child: Column(mainAxisAlignment: MainAxisAlignment.center, children: [
                LottieBuilder.asset(
                  'assets/animations/no_internet.json',
                  height: 240,
                ),
                Text(
                  LocalKeys.oops,
                  style: Theme.of(context)
                      .textTheme
                      .titleLarge
                      ?.copyWith(fontWeight: FontWeight.bold),
                ),
                8.toHeight,
                Text(
                  LocalKeys.noConnectionFound,
                  style: Theme.of(context).textTheme.bodyMedium,
                ),
                16.toHeight,
                CustomButton(
                    onPressed: () {
                      Navigator.pop(context);
                    },
                    btText: LocalKeys.okay,
                    isLoading: false)
              ]),
            ),
          ),
        ).then((value) => isOffline = false);
      }
    });
  }
}

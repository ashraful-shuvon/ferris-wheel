// WebGL <-> Flutter WebView bridge for Zimo Live games.
//
// Two jobs:
//   1. Expose the launch query params (token / api) that the Flutter host
//      appends to the WebGL page URL, so Unity can authenticate + know which
//      backend to call.
//   2. Forward a "close" request from the in-game close button up to the
//      Flutter host via the `ZimoGameBridge` JavaScript channel (the contract
//      already implemented in game_page.dart).
//
// Strings returned to C# must be copied into the Unity heap with _malloc +
// stringToUTF8 (the standard Unity WebGL marshalling pattern).
mergeInto(LibraryManager.library, {
  // Returns the value of a single query-string param (or "" if absent).
  ZimoGetUrlParam: function (namePtr) {
    var name = UTF8ToString(namePtr);
    var value = '';
    try {
      value = new URLSearchParams(window.location.search).get(name) || '';
    } catch (e) {
      value = '';
    }
    var size = lengthBytesUTF8(value) + 1;
    var buffer = _malloc(size);
    stringToUTF8(value, buffer, size);
    return buffer;
  },

  // Ask the Flutter host to close the WebView. Uses the ZimoGameBridge
  // JavaScript channel registered by game_page.dart; falls back to no-op in a
  // plain browser.
  ZimoRequestClose: function () {
    try {
      if (window.ZimoGameBridge && window.ZimoGameBridge.postMessage) {
        window.ZimoGameBridge.postMessage(JSON.stringify({ type: 'close' }));
      }
    } catch (e) {
      // Not embedded — ignore.
    }
  },

  // Generic host message: the game posts `{ type, ...payload }` and the Flutter
  // side decides what it means (close the sheet, open the wallet, …). Adding a
  // new in-game action is then a C# call plus a case in the host's handler —
  // no new jslib binding, no rebuild of the bridge contract.
  //
  // Takes the full JSON so payloads (amounts, ids) can travel later without
  // changing this signature.
  ZimoPostMessage: function (jsonPtr) {
    try {
      var json = UTF8ToString(jsonPtr);
      if (window.ZimoGameBridge && window.ZimoGameBridge.postMessage) {
        window.ZimoGameBridge.postMessage(json);
      }
    } catch (e) {
      // Not embedded — ignore.
    }
  },
});

mergeInto(LibraryManager.library, {
  SendSelectedObject: function (objectId, size) {
    var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';
    window.sendSelectedObjectFromUnity(UTF8ToString(objectId), instanceId);
  },
  
  SendSceneReady: function () {
    var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';
    window.sendSceneReadyFromUnity(instanceId);
  },
  
  SendObjectsList: function (objectIds, size) {
    var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';
    window.sendObjectsListFromUnity(UTF8ToString(objectIds), instanceId);
  },
  
  SendSceneState: function (json, size) {
    var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';
    window.sendSceneStateFromUnity(UTF8ToString(json), instanceId);
  },
  
  RequestDataFromWeb: function (queryPtr, callbackPtr) {
    var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';
    var query = UTF8ToString(queryPtr);
    window.requestDataFromWebFromUnity(query, function (result) {
      var bufferSize = lengthBytesUTF8(result) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(result, buffer, bufferSize);
      {{{ makeDynCall('vi', 'callbackPtr') }}}(buffer);
      _free(buffer);
    }, instanceId);
  },
  
  RegisterOnNavigationChanged: function (callbackPtr) {
    var instanceId = (typeof Module !== 'undefined' && Module['canvas'] && Module['canvas'].id) || 'default';
    window.registerOnNavigationChangedFromUnity(function (data) {
      var bufferSize = lengthBytesUTF8(data) + 1;
      var buffer = _malloc(bufferSize);
      stringToUTF8(data, buffer, bufferSize);
      {{{ makeDynCall('vi', 'callbackPtr') }}}(buffer);
      _free(buffer);
    }, instanceId);
  },
  
});

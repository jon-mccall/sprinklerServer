// Use the same device that served this page. The REST API listens on port 8888
// while the web UI listens on port 8889.
var hostAndPort = window.location.protocol + "//" + window.location.hostname + ":8888";
var webHost = window.location.protocol + "//" + window.location.host;
var onOffList = new Array(false, false, false, false, false, false, false, false, false, false, false, false, false, false, false);

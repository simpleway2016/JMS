
function JmsServiceFinder(gatewayUrls) {
    function createRequest(callback) {
        var http = new XMLHttpRequest();
        http.onreadystatechange = function () {
            if (http.readyState == 4) {
                if (http.status >= 200 && http.status < 300) {
                    callback(http.responseText);
                }
                else {
                    callback(null, { status: http.status });
                }
            }
        };
        http.onerror = function (e) {
            callback(null, { err: e });

        };
        http.ontimeout = function () {
            callback(null, { err: "timeout" });
        };
        return http;
    }

    function GatewayFinder(gatewayAddresses) {
        function checkGateway(gatewayUrl, resolve, reject) {
            var http = createRequest(function (ret, err) {
                if (ret) {
                    ret = eval("ret = " + ret);
                    if (ret.Success) {
                        resolve(gatewayUrl);
                    }
                    else {
                        resolve(false);
                    }
                }
                else {
                    reject(err);
                }
            });

            http.open("GET", gatewayUrl + "/?FindMaster", true);
            http.send(null);
        }
        this.find = async function () {
            return new Promise(function (resolve, reject) {
                if (Array.isArray(gatewayAddresses)) {
                    if (gatewayAddresses.length == 1) {
                        resolve(gatewayAddresses[0]);
                    }
                    else {
                        var errs = [];
                        for (var i = 0; i < gatewayAddresses.length; i++) {
                            checkGateway(gatewayAddresses[i], function (ret) {
                                if (ret) {
                                    resolve(ret);
                                }
                            }, function (err) {
                                errs.push(err);
                                if (errs.length == gatewayAddresses.length) {
                                    reject(errs[0]);
                                }
                            });
                        }
                    }
                }
                else {
                    return gatewayAddresses;
                }
            });
        }
    }

    var gatewayUrl = null;
    var gatewayError = null;
    var gatewayFinder = new GatewayFinder(gatewayUrls);
    function findMaster() {
        gatewayFinder.find().then(function (ret) {
            gatewayError = null;
            gatewayUrl = ret;
            window.setTimeout(findMaster, 3000);
        }).catch(function (err) {
            gatewayError = err;
            window.setTimeout(findMaster, 3000);
        });
    }

    findMaster();

    function internalGetService(serviceName, resolve, reject) {
        if (!gatewayError && !gatewayUrl) {
            window.setTimeout(function () {
                internalGetService(serviceName, resolve, reject);
            }, 500);
            return;
        }

        if (gatewayError) {
            reject(gatewayError);
            return;
        }

        var http = createRequest(function (ret, err) {
            if (ret) {
                ret = eval("ret = " + ret);
                resolve(ret.ServiceAddress);
            }
            else {
                reject(err);
            }
        });
        http.open("GET", gatewayUrl + "/?GetServiceProvider=" + encodeURIComponent(serviceName), true);
        http.send(null);
    }

    this.getService = async function (serviceName) {

        return new Promise(function (resolve, reject) {
            internalGetService(serviceName, resolve, reject);
        });
    }
}
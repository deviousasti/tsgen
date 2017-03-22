///<reference path="Namespace.js" />
/// <reference path="rx.min.js"/>
/* Service.js 
   
Contains base types of all proxy / entity classes as well
as dependency/inheritance tracking.  
*/

(function (Base) {

    if (typeof (Base) == "undefined") return;
    if (Base.Application) return;

    if (typeof (Base.Namespace) == "undefined")
        throw "Namespace.js is not included!";

    Namespace.Dependencies = new Array();
    Namespace.Resolve = function (func) {
        if (func == null) {
            while (Namespace.Dependencies.length > 0) {
                func = Namespace.Dependencies.pop();
                func.call(window);
            }

        } else {
            Namespace.Dependencies.push(func);
        }
    };

    Namespace.Extend = function (constructor, extender) {
        extender.call(constructor.prototype, constructor.Base);
    };

    Namespace.Inherits = function (parent, derived) {
        derived.Base = parent;
        derived.prototype = new parent();
        derived.prototype.constructor = derived;
        return derived;
    };

    Namespace.Supercedes = function (constructor, extender) {
        for (var i = 0; i < constructor.Descendents.length; i++) {
            Namespace.Extend(constructor.Descendents[i], extender);
        }
    }

    String.Format = function (str) {
        var args = arguments;
        var pattern = new RegExp(/\{([0-9]+)\}/g);
        return String(str).replace(pattern, function (match, index) {
            index = parseInt(index, 10);

            if (index == args.length - 1)
                throw "Index is zero based. Must be greater than 0 and less than " + (args.length - 1);

            return args[(index + 1)];
        });
    };

    Object.isSet = function (value) {
        return typeof value != "undefined";
    };

  

    Base.Application = (function () {

        var Application = { __namespace: true, Settings: { ServicePath: '/' } };

        Application.Util = {
            ServiceRequest: function (httpMethod, url, data, success, fail, async) {

                var result = null;

                $.ajax({
                    type: httpMethod,
                    contentType: "application/json; charset=utf-8",
                    url: Application.Settings.ServicePath + url,
                    data: httpMethod == "GET" ? data : JSON.stringify(data),
                    dataType: "json",
                    async: async,
                    success: function (message) {
                        var d = message.d;

                        if (success != null)
                            success.call(d, d);

                        result = d;
                    }
                });

                return result;
            },

            Cast: function (source, type) {

                if (source == null) return null;
                if (source instanceof type) return source;

                var newObj = new type();
                var proto = type.prototype;

                for (var key in proto) {

                    if (typeof proto[key] == "function" || typeof source[key] == 'undefined') continue;

                    if (proto[key] instanceof Object)
                        newObj[key] = $.cast(source[key], proto[key].constructor)
                    else
                        newObj[key] = source[key];
                }

                return newObj;

            },

            Guid: function () {
                // http://www.ietf.org/rfc/rfc4122.txt
                var s = [];
                var hexDigits = "0123456789abcdef";
                for (var i = 0; i < 36; i++) {
                    s[i] = hexDigits.substr(Math.floor(Math.random() * 0x10), 1);
                }
                s[14] = "4";  // bits 12-15 of the time_hi_and_version field to 0010
                s[19] = hexDigits.substr((s[19] & 0x3) | 0x8, 1);  // bits 6-7 of the clock_seq_hi_and_reserved to 01
                s[8] = s[13] = s[18] = s[23] = "-";

                var uuid = s.join("");
                return uuid;
            }
        };

        Application.Event = (function () {

            var Event = function (target) {
                this.handlers = [];
                this.target = target == null ? this : target;
            };

            Event.prototype.name = "event";
            Event.prototype.__event = true;
            Event.prototype.handlers = [];
            Event.prototype.target = null;

            Event.prototype.addEventListener = function (handler) {
                if (handler instanceof Function)
                    this.handlers.push(handler);
            };

            Event.prototype.removeEventListener = function (handler) {
                var index;

                for (index = 0;
                 index < this.handlers.length || this.handlers[index] === handler;
                 index++);

                return this.handlers.splice(index, 1);
            };

            Event.prototype.trigger = function () {
                for (var i = 0; i < this.handlers.length; i++) {
                    if (this.handlers[i] == null) continue;

                    this.handlers[i].apply(this.target, arguments);
                }
            };

            return Event;
        })();

        Application.Entity = (function () {

            var Entity = function () { };

            Entity.prototype = new Object();
            Entity.prototype.__type = "Application.Entity";
            Entity.prototype.constructor = Entity;
            Entity.Descendents = [];

            return Entity;
        })();

        Application.ViewModel = (function () {

            var VM = function () { };

            VM.prototype.__type = "Application.ViewModel";
            VM.prototype = new Application.Entity();
            VM.prototype.constructor = Application.ViewModel;
            VM.Descendents = [];

            VM.prototype.OnPropertyChanged = function (name, value) {

            };

            VM.prototype.Subscribe = function (propertyName, handler) {
                this["$" + propertyName].subscribe(handler, this);
            };

            VM.prototype.NotifyPropertyChange = function (propertyName, value) {
                this["$" + propertyName].notifySubscribers(value);
            };

            VM.prototype.TemplateSelector = function (obj) {
                var context = "";
                if (obj.TemplateContext) context = obj.TemplateContext();
                if (obj.GetType) return obj.GetType() + context;
            };

            VM.prototype.Bind = function (context) {
                if (jQuery instanceof Function)
                    context = jQuery(context).get(0);

                ko.applyBindings(this, context);
            };

            return VM;
        })();

        Application.SocketLayer = (function () {

            var SocketLayer = function (target) {
                this.uri = null;
                this.socket = null;
                this.target = target;
                this.onconnect = new Application.Event();
                this.onclose = new Application.Event();
                this.onerror = new Application.Event();
                this.onmessage = new Application.Event();
                this.subscriptions = {};
                this.callbacks = {};
            };

            SocketLayer.prototype = new Object();
            SocketLayer.prototype.constructor = SocketLayer;
            SocketLayer.Descendents = [];

            SocketLayer.prototype.callbacks = {};

            SocketLayer.prototype.isConnected = false;
            SocketLayer.prototype.connect = function (uri) {

                var socket = null;

                if (window.WebSocket)
                    socket = new WebSocket(uri);
                else
                    if (window.MozWebSocket)
                        socket = new MozWebSocket(uri);
                    else
                        throw "WebSocket not supported!";

                if (socket != null) {
                    var root = this;

                    this.socket = socket;
                    this.uri = uri;

                    socket.onopen = function (evt) { root.isConnected = true; root.onconnect.trigger(evt); };
                    socket.onclose = function (evt) { root.isConnected = false; root.onclose.trigger(evt); };
                    socket.onerror = function (evt) { root.isConnected = false; root.onerror.trigger(evt); };

                    socket.onmessage = function (evt) {
                        root.receive.call(root, evt);
                        root.onmessage.trigger(evt);
                    };
                }
            };

            SocketLayer.prototype.reconnect = function () {
                this.connect(this.uri);
            };

            SocketLayer.prototype.methodCall = function (type, name, args, callbackId) {

                if (!this.isConnected) return;

                if (!callbackId) callbackId = null;

                var jsonArgs = [];

                for (var i = 0; i < args.length; i++)
                    jsonArgs.push(JSON.stringify(args[i]));

                var pack = { Type: type, Name: name, Args: jsonArgs, Callback: callbackId };
                var packJSON = JSON.stringify(pack);

                this.socket.send(packJSON);
            };

            SocketLayer.prototype.registerCallback = function (callback) {
                if (callback == null)
                    return null;

                var callbackId = Application.Util.Guid();
                this.callbacks[callbackId] = callback;

                return callbackId;
            };

            SocketLayer.prototype.rpcCall = function (name, args) {

                var parameters = Array.prototype.slice.call(args, null);

                var callback = parameters.pop();

                if (!(callback instanceof Function)) {
                    //undo
                    if (callback != null)
                        parameters.push(callback);
                }

                this.methodCall('method', name, parameters, this.registerCallback(callback));

            };

            SocketLayer.prototype.receive = function (e) {
                var data = JSON.parse(e.data);

                switch (data.Type) {

                    case "method":

                        var method = this.target[data.Name];
                        var args = data.Args;

                        if (method instanceof Application.Event)
                            method.trigger(objArgs);
                        else
                            method.apply(this.target, args);

                        break;

                    case "callback":

                        var callback = this.callbacks[data.Callback];
                        if (callback instanceof Function) {
                            delete this.callbacks[data.Callback];
                            callback(data.Result);
                        }

                        break;

                    case "notification":

                        var notify = this.subscriptions[data.Route];

                        if (notify instanceof Rx.Observer) {
                            var notification;

                            switch (data.Kind) {
                                case "N": notification = Rx.Notification.createOnNext(data.Value);
                                    break;

                                case "C": notification = Rx.Notification.createOnCompleted();
                                    break;

                                case "E": notification = Rx.Notification.createOnError(data.Error);
                                    break;

                                default:
                                    //shouldn't be here
                                    throw "Unknown notification message type";
                            }

                            notification.accept(notify);

                            return;
                        }

                        if (notify instanceof Function) {
                            notify(data.Data);
                            return;
                        }

                        break;

                    default:

                }

            };

            return SocketLayer;

        })();

        Application.Proxy = (function () {

            var Proxy = function () {
                this.instance = new Application.SocketLayer(this);
            };

            Proxy.prototype = new Object();
            Proxy.prototype.instance = null;
            Proxy.prototype.constructor = Proxy;
            Proxy.Descendents = [];

            Proxy.prototype.Connect = function (uri, connected, closed) {

                if (this.instance.isConnected)
                    return;

                if (connected instanceof Function)
                    this.OnConnect(connected);

                if (closed instanceof Function)
                    this.instance.onerror.addEventListener(closed);

                this.instance.connect(uri);

            };

            Proxy.prototype.OnConnect = function (handler) {
                this.instance.onconnect.addEventListener(handler);
            };

            Proxy.prototype.OnError = function (handler) {
                this.instance.onerror.addEventListener(handler);
            };

            Proxy.prototype.IsConnected = function () {
                return this.instance.isConnected;
            };

            Proxy.prototype.KeepReconnecting = function (uri, interval, onreconnect) {
                interval = interval || 3000;

                var callback = function () {

                    if (!this.IsConnected()) {

                        if (onreconnect instanceof Function)
                            onreconnect.call(this);

                        this.instance.connect(uri);
                    }

                }.bind(this);

                var handle = window.setInterval(callback, interval);

                callback();

                return Rx.Disposable.create(function () {
                    window.clearInterval(handle);
                });
            };

            Proxy.prototype.Subscribe = function () {

                if (arguments.length == 1) {
                    var topic = arguments[0];
                    var proxy = this;
                    return Rx.Observable.createWithDisposable(function (observer) { return proxy.Subscribe(observer, topic); });
                }

                if (arguments.length == 2) {
                    var observer = arguments[0];
                    var topic = arguments[1];

                    var unsub = Application.Util.Guid();

                    if (observer instanceof Function)
                        observer = Rx.Observer.create(observer);

                    this.instance.subscriptions[unsub] = observer;

                    //Inform host
                    this.instance.methodCall("subscribe", "Subscribe", [topic, unsub]);

                    return Rx.Disposable.create(function () {
                        delete this.instance.subscriptions[unsub];

                        //Inform host
                        this.instance.methodCall("unsubscribe", "Unsubscribe", [unsub]);
                    });
                }

                throw "The call must be either Subscribe(topic) or Subscribe(fn|observer, topic)";

            };

            Proxy.prototype.Connectable = function (topic) {
                var subject = new Rx.Subject();
                var observer = Rx.Observer.create(subject.onNext, subject.onError, subject.onCompleted);

                var connectable = subject.asObservable();

                connectable.Dispose = this.Subscribe(observer, topic).Dispose;

                return connectable;
            };

            Proxy.prototype.Publish = function () {
                var topic, data;

                if (arguments.length == 1) {
                    topic = arguments[0];
                    var proxy = this;

                    return Rx.Observer.create(function (value) { proxy.Publish(value, topic); },
                                          function (err) { proxy.Publish(new Rx.Notification("E", err), topic); },
                                          function () { proxy.Publish(new Rx.Notification("C"), topic); }
                                          );
                }

                if (arguments.length == 2) {
                    data = arguments[0];
                    topic = arguments[1];

                    if (data instanceof Function) {
                        return data(this.Publish(topic));
                    }

                    if (data instanceof Rx.Notification) {
                        return this.instance.methodCall("notification", "Publish", [topic, data.Value, data.Kind]);
                    }

                    if (data instanceof Rx.Observable) {
                        return data.subscribe(this.Publish(topic));
                    }

                    return this.instance.methodCall("publish", "Publish", [topic, data]);
                }

                throw "The call must be either Publish(topic) or Publish(item|sequence, topic)";

            };

            return Proxy;

        })();


        return Application;

    })();

})(window);

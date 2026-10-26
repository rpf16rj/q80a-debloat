const fs=require('fs'),path=require('path');
const forge=require('node-forge'),JSZip=require('jszip');
const {Signature}=require('tizen');
const ROOT=path.resolve('../../wgt3');
const AUTHOR_P12='C:/Users/rpf16/AppData/Local/Temp/author-tb.p12';
const DIST_P12='C:/tizen-studio/tools/certificate-generator/certificates/distributor/tizen-distributor-signer.p12';
const AUTHOR_PW=process.env.AUTHOR_PW, DIST_PW=process.env.DIST_PW;
function loadP12(f,pw){return forge.pkcs12.pkcs12FromAsn1(forge.asn1.fromDer(fs.readFileSync(f).toString('binary')),false,pw);}
const walk=d=>fs.readdirSync(d,{withFileTypes:true}).flatMap(e=>e.isDirectory()?walk(path.join(d,e.name)):[path.join(d,e.name)]);
(async()=>{
  const author=loadP12(AUTHOR_P12,AUTHOR_PW),dist=loadP12(DIST_P12,DIST_PW);
  const all=walk(ROOT).filter(f=>!f.endsWith('.manifest.tmp')).map(f=>({uri:encodeURIComponent(path.relative(ROOT,f).split(path.sep).join('/')),data:fs.readFileSync(f)}));
  const authored=await new Signature('AuthorSignature',all).sign(author);
  const signed=await new Signature('DistributorSignature',authored).sign(dist);
  const zip=new JSZip();
  for(const f of signed)zip.file(decodeURIComponent(f.uri),f.data);
  const buf=await zip.generateAsync({type:'nodebuffer',compression:'DEFLATE'});
  fs.writeFileSync('../../wledAmbi2.wgt',buf);
  console.log('signed -> wledAmbi2.wgt',buf.length);
})().catch(e=>{console.log('ERR',e.message)});

const fs=require('fs'),path=require('path');
const forge=require('node-forge'),JSZip=require('jszip');
const {Signature}=require('tizen');
const ROOT=path.resolve('../../wgt2');
const AUTHOR_P12='C:/Users/rpf16/Desktop/cert samsung tyzen/author.p12';
const DIST_P12='C:/Users/rpf16/SamsungCertificate/q80a/distributor.p12';
const PW=process.env.CERT_PW;
function loadP12(f,pw){return forge.pkcs12.pkcs12FromAsn1(forge.asn1.fromDer(fs.readFileSync(f).toString('binary')),false,pw);}
(async()=>{
  const author=loadP12(AUTHOR_P12,PW),dist=loadP12(DIST_P12,PW);
  const files=fs.readdirSync(ROOT).filter(f=>fs.statSync(path.join(ROOT,f)).isFile()).map(f=>({uri:encodeURIComponent(f),data:fs.readFileSync(path.join(ROOT,f))}));
  const authored=await new Signature('AuthorSignature',files).sign(author);
  const signed=await new Signature('DistributorSignature',authored).sign(dist);
  const zip=new JSZip();
  for(const f of signed)zip.file(decodeURIComponent(f.uri),f.data);
  const buf=await zip.generateAsync({type:'nodebuffer',compression:'DEFLATE'});
  fs.writeFileSync('../../wledAmbiUi.wgt',buf);
  console.log('signed -> wledAmbiUi.wgt',buf.length);
})().catch(e=>{console.log('ERR',e.message)});

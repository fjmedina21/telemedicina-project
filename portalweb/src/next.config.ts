import type {NextConfig} from "next";
const nextConfig:NextConfig={
  reactStrictMode:true,
  outputFileTracingRoot:__dirname,
  async rewrites(){return [{
    source:"/api/backend/:path*",
    destination:(process.env.API_INTERNAL_URL||"http://127.0.0.1:5000/api/v1")+"/:path*"
  }];}
};
export default nextConfig;

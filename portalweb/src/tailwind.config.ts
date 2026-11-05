import type {Config} from "tailwindcss";
const config:Config={
  content:["./src/**/*.{js,ts,jsx,tsx,mdx}"],
  theme:{extend:{colors:{
    primary:{50:"#f5f7f4",100:"#e5f0e9",500:"#39715d",600:"#205c4d",700:"#173f35"},
    clinical:{teal:"#205c4d",blue:"#28566a",navy:"#172e27"},
  }}},plugins:[],
};
export default config;

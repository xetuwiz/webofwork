import { useMutation } from "@tanstack/react-query";
import { switchRole } from "./homePageApi.js";
export const useSwitchRole = () => useMutation({ mutationFn: switchRole });

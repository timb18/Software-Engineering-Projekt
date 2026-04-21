--
-- PostgreSQL database dump
--

\restrict 1cpS6kNaBSDwTIHQwWEfbwu6uBP6cp5BAXlRcnV4k4Au5O9yaZHinxsbAH2T4hz

-- Dumped from database version 18.3 (Debian 18.3-1.pgdg13+1)
-- Dumped by pg_dump version 18.3

-- Started on 2026-04-21 21:56:50

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

DROP DATABASE "teapot-db";
--
-- TOC entry 3522 (class 1262 OID 16384)
-- Name: teapot-db; Type: DATABASE; Schema: -; Owner: postgres
--

CREATE DATABASE "teapot-db" WITH TEMPLATE = template0 ENCODING = 'UTF8' LOCALE_PROVIDER = libc LOCALE = 'en_US.utf8';


ALTER DATABASE "teapot-db" OWNER TO postgres;

\unrestrict 1cpS6kNaBSDwTIHQwWEfbwu6uBP6cp5BAXlRcnV4k4Au5O9yaZHinxsbAH2T4hz
\encoding SQL_ASCII
\connect -reuse-previous=on "dbname='teapot-db'"
\restrict 1cpS6kNaBSDwTIHQwWEfbwu6uBP6cp5BAXlRcnV4k4Au5O9yaZHinxsbAH2T4hz

SET statement_timeout = 0;
SET lock_timeout = 0;
SET idle_in_transaction_session_timeout = 0;
SET transaction_timeout = 0;
SET client_encoding = 'UTF8';
SET standard_conforming_strings = on;
SELECT pg_catalog.set_config('search_path', '', false);
SET check_function_bodies = false;
SET xmloption = content;
SET client_min_messages = warning;
SET row_security = off;

--
-- TOC entry 5 (class 2615 OID 16476)
-- Name: public; Type: SCHEMA; Schema: -; Owner: postgres
--

CREATE SCHEMA public;


ALTER SCHEMA public OWNER TO postgres;

--
-- TOC entry 872 (class 1247 OID 16574)
-- Name: invitation_status; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.invitation_status AS ENUM (
    'open',
    'closed',
    'accepted',
    'expired'
);


ALTER TYPE public.invitation_status OWNER TO postgres;

--
-- TOC entry 890 (class 1247 OID 16549)
-- Name: role; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.role AS ENUM (
    'user',
    'organizer'
);


ALTER TYPE public.role OWNER TO postgres;

--
-- TOC entry 884 (class 1247 OID 16651)
-- Name: task_intensity; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.task_intensity AS ENUM (
    'light',
    'normal',
    'intensive'
);


ALTER TYPE public.task_intensity OWNER TO postgres;

--
-- TOC entry 881 (class 1247 OID 16615)
-- Name: task_priority; Type: TYPE; Schema: public; Owner: postgres
--

CREATE TYPE public.task_priority AS ENUM (
    'low',
    'medium',
    'high'
);


ALTER TYPE public.task_priority OWNER TO postgres;

SET default_tablespace = '';

SET default_table_access_method = heap;

--
-- TOC entry 224 (class 1259 OID 16590)
-- Name: invitations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.invitations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    organization_id uuid NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone,
    status public.invitation_status NOT NULL,
    expiry_date timestamp with time zone
);


ALTER TABLE public.invitations OWNER TO postgres;

--
-- TOC entry 223 (class 1259 OID 16553)
-- Name: memberships; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.memberships (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    user_id uuid NOT NULL,
    organization_id uuid NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone,
    role public.role NOT NULL
);


ALTER TABLE public.memberships OWNER TO postgres;

--
-- TOC entry 220 (class 1259 OID 16488)
-- Name: organizations; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.organizations (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(100) NOT NULL,
    max_users integer NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone
);


ALTER TABLE public.organizations OWNER TO postgres;

--
-- TOC entry 226 (class 1259 OID 16634)
-- Name: task_blocks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.task_blocks (
    task_id uuid CONSTRAINT task_timeblocks_task_id_not_null NOT NULL,
    start_date timestamp with time zone CONSTRAINT task_timeblocks_start_date_not_null NOT NULL,
    end_date timestamp with time zone CONSTRAINT task_timeblocks_end_date_not_null NOT NULL,
    is_fixed boolean CONSTRAINT task_timeblocks_is_fixed_not_null NOT NULL
);


ALTER TABLE public.task_blocks OWNER TO postgres;

--
-- TOC entry 227 (class 1259 OID 16684)
-- Name: task_dependencies; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.task_dependencies (
    task_id uuid NOT NULL,
    depends_on_task_id uuid NOT NULL
);


ALTER TABLE public.task_dependencies OWNER TO postgres;

--
-- TOC entry 225 (class 1259 OID 16621)
-- Name: user_tasks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.user_tasks (
    id uuid DEFAULT gen_random_uuid() CONSTRAINT tasks_id_not_null NOT NULL,
    work_profile_id uuid CONSTRAINT tasks_work_profile_id_not_null NOT NULL,
    description character varying(255),
    priority public.task_priority CONSTRAINT tasks_priority_not_null NOT NULL,
    is_fixed boolean CONSTRAINT tasks_is_fixed_not_null NOT NULL,
    time_estimate interval CONSTRAINT tasks_time_estimate_not_null NOT NULL,
    deadline timestamp with time zone,
    created_at timestamp with time zone DEFAULT now() CONSTRAINT tasks_created_at_not_null NOT NULL,
    edited_at timestamp with time zone,
    intensity public.task_intensity NOT NULL,
    name character varying(30) NOT NULL,
    early_start timestamp with time zone NOT NULL,
    early_finish timestamp with time zone NOT NULL,
    late_start timestamp with time zone NOT NULL,
    late_finish timestamp with time zone NOT NULL
);


ALTER TABLE public.user_tasks OWNER TO postgres;

--
-- TOC entry 219 (class 1259 OID 16477)
-- Name: users; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.users (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    username character varying(255),
    email character varying(40) NOT NULL,
    created_at timestamp with time zone DEFAULT now() NOT NULL,
    edited_at timestamp with time zone,
    CONSTRAINT valid_email CHECK (((email)::text ~* '^[A-Za-z0-9._+%-]+@[A-Za-z0-9.-]+[.][A-Za-z]+$'::text))
);


ALTER TABLE public.users OWNER TO postgres;

--
-- TOC entry 222 (class 1259 OID 16537)
-- Name: work_profile_time_intervals; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.work_profile_time_intervals (
    work_profile_id uuid CONSTRAINT time_intervals_work_profile_id_not_null NOT NULL,
    start_date timestamp with time zone CONSTRAINT time_intervals_start_date_not_null NOT NULL,
    end_date timestamp with time zone CONSTRAINT time_intervals_end_date_not_null NOT NULL
);


ALTER TABLE public.work_profile_time_intervals OWNER TO postgres;

--
-- TOC entry 221 (class 1259 OID 16520)
-- Name: work_profiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.work_profiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    membership_id uuid NOT NULL,
    max_daily_load interval NOT NULL,
    planner_view_start character varying(5) NOT NULL DEFAULT '06:00',
    planner_view_end character varying(5) NOT NULL DEFAULT '22:00',
    created_at timestamp with time zone NOT NULL,
    edited_at timestamp with time zone
);

--
-- Name: work_day_profiles; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.work_day_profiles (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    work_profile_id uuid NOT NULL,
    day character varying(3) NOT NULL
);

ALTER TABLE public.work_day_profiles OWNER TO postgres;

--
-- Name: work_blocks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.work_blocks (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    work_day_profile_id uuid NOT NULL,
    company_id character varying NOT NULL DEFAULT '',
    company_name character varying NOT NULL DEFAULT '',
    start_time character varying(5) NOT NULL DEFAULT '09:00',
    end_time character varying(5) NOT NULL DEFAULT '17:00'
);

ALTER TABLE public.work_blocks OWNER TO postgres;

--
-- Name: work_breaks; Type: TABLE; Schema: public; Owner: postgres
--

CREATE TABLE public.work_breaks (
    id uuid DEFAULT gen_random_uuid() NOT NULL,
    work_day_profile_id uuid NOT NULL,
    start_time character varying(5) NOT NULL DEFAULT '12:00',
    end_time character varying(5) NOT NULL DEFAULT '12:30'
);

ALTER TABLE public.work_breaks OWNER TO postgres;


ALTER TABLE public.work_profiles OWNER TO postgres;

--
-- TOC entry 3357 (class 2606 OID 16601)
-- Name: invitations invitations_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invitations
    ADD CONSTRAINT invitations_pkey PRIMARY KEY (id);


--
-- TOC entry 3355 (class 2606 OID 16562)
-- Name: memberships memberships_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.memberships
    ADD CONSTRAINT memberships_pkey PRIMARY KEY (id);


--
-- TOC entry 3349 (class 2606 OID 16498)
-- Name: organizations organizations_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.organizations
    ADD CONSTRAINT organizations_pkey PRIMARY KEY (id);


--
-- TOC entry 3359 (class 2606 OID 16633)
-- Name: user_tasks tasks_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_tasks
    ADD CONSTRAINT tasks_pkey PRIMARY KEY (id);


--
-- TOC entry 3345 (class 2606 OID 16487)
-- Name: users users_email_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_email_key UNIQUE (email);


--
-- TOC entry 3347 (class 2606 OID 16485)
-- Name: users users_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.users
    ADD CONSTRAINT users_pkey PRIMARY KEY (id);


--
-- TOC entry 3351 (class 2606 OID 16531)
-- Name: work_profiles work_profiles_membership_id_key; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_profiles
    ADD CONSTRAINT work_profiles_membership_id_key UNIQUE (membership_id);


--
-- TOC entry 3353 (class 2606 OID 16529)
-- Name: work_profiles work_profiles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_profiles
    ADD CONSTRAINT work_profiles_pkey PRIMARY KEY (id);


--
-- Name: work_day_profiles work_day_profiles_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_day_profiles
    ADD CONSTRAINT work_day_profiles_pkey PRIMARY KEY (id);


--
-- Name: work_blocks work_blocks_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_blocks
    ADD CONSTRAINT work_blocks_pkey PRIMARY KEY (id);


--
-- Name: work_breaks work_breaks_pkey; Type: CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_breaks
    ADD CONSTRAINT work_breaks_pkey PRIMARY KEY (id);


--
-- TOC entry 3364 (class 2606 OID 16607)
-- Name: invitations invitations_created_by_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invitations
    ADD CONSTRAINT invitations_created_by_fkey FOREIGN KEY (created_by) REFERENCES public.users(id);


--
-- TOC entry 3365 (class 2606 OID 16602)
-- Name: invitations invitations_organization_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.invitations
    ADD CONSTRAINT invitations_organization_id_fkey FOREIGN KEY (organization_id) REFERENCES public.organizations(id);


--
-- TOC entry 3362 (class 2606 OID 16568)
-- Name: memberships memberships_organization_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.memberships
    ADD CONSTRAINT memberships_organization_id_fkey FOREIGN KEY (organization_id) REFERENCES public.organizations(id);


--
-- TOC entry 3363 (class 2606 OID 16563)
-- Name: memberships memberships_user_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.memberships
    ADD CONSTRAINT memberships_user_id_fkey FOREIGN KEY (user_id) REFERENCES public.users(id);


--
-- TOC entry 3368 (class 2606 OID 16694)
-- Name: task_dependencies task_dependencies_depends_on_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_dependencies
    ADD CONSTRAINT task_dependencies_depends_on_task_id_fkey FOREIGN KEY (depends_on_task_id) REFERENCES public.user_tasks(id);


--
-- TOC entry 3369 (class 2606 OID 16689)
-- Name: task_dependencies task_dependencies_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_dependencies
    ADD CONSTRAINT task_dependencies_task_id_fkey FOREIGN KEY (task_id) REFERENCES public.user_tasks(id);


--
-- TOC entry 3367 (class 2606 OID 16640)
-- Name: task_blocks task_timeslots_task_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.task_blocks
    ADD CONSTRAINT task_timeslots_task_id_fkey FOREIGN KEY (task_id) REFERENCES public.user_tasks(id);


--
-- TOC entry 3366 (class 2606 OID 16645)
-- Name: user_tasks tasks_workprofile_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.user_tasks
    ADD CONSTRAINT tasks_workprofile_id_fkey FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id) NOT VALID;


--
-- TOC entry 3361 (class 2606 OID 16543)
-- Name: work_profile_time_intervals time_intervals_work_profile_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_profile_time_intervals
    ADD CONSTRAINT time_intervals_work_profile_id_fkey FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id);


--
-- TOC entry 3360 (class 2606 OID 16678)
-- Name: work_profiles work_profiles_membership_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_profiles
    ADD CONSTRAINT work_profiles_membership_id_fkey FOREIGN KEY (membership_id) REFERENCES public.memberships(id) NOT VALID;


--
-- Name: work_day_profiles work_day_profiles_work_profile_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_day_profiles
    ADD CONSTRAINT work_day_profiles_work_profile_id_fkey FOREIGN KEY (work_profile_id) REFERENCES public.work_profiles(id) ON DELETE CASCADE;


--
-- Name: work_blocks work_blocks_work_day_profile_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_blocks
    ADD CONSTRAINT work_blocks_work_day_profile_id_fkey FOREIGN KEY (work_day_profile_id) REFERENCES public.work_day_profiles(id) ON DELETE CASCADE;


--
-- Name: work_breaks work_breaks_work_day_profile_id_fkey; Type: FK CONSTRAINT; Schema: public; Owner: postgres
--

ALTER TABLE ONLY public.work_breaks
    ADD CONSTRAINT work_breaks_work_day_profile_id_fkey FOREIGN KEY (work_day_profile_id) REFERENCES public.work_day_profiles(id) ON DELETE CASCADE;


--
-- TOC entry 3523 (class 0 OID 0)
-- Dependencies: 5
-- Name: SCHEMA public; Type: ACL; Schema: -; Owner: postgres
--

REVOKE USAGE ON SCHEMA public FROM PUBLIC;
GRANT ALL ON SCHEMA public TO PUBLIC;


-- Completed on 2026-04-21 21:56:50

--
-- PostgreSQL database dump complete
--

\unrestrict 1cpS6kNaBSDwTIHQwWEfbwu6uBP6cp5BAXlRcnV4k4Au5O9yaZHinxsbAH2T4hz

